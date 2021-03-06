﻿using Grpc.Core;
using Startrek;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using static Startrek.Transporter;

namespace XamlBrewer.Uwp.Grpc.Client
{
    public sealed partial class MainPage : Page
    {
        private Channel _channel;
        private TransporterClient _client;

        private Random _rnd = new Random(DateTime.Now.Millisecond);

        public MainPage()
        {
            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = true;

            var titleBar = ApplicationView.GetForCurrentView().TitleBar;
            if (titleBar != null)
            {
                titleBar.BackgroundColor = Colors.Transparent;
                titleBar.ForegroundColor = Color.FromArgb(255, 214, 117, 36);
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.SlateGray;
                titleBar.ButtonForegroundColor = Color.FromArgb(255, 214, 117, 36);
            }

            this.InitializeComponent();

            Loaded += MainPage_Loaded;
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Just to get rid of the async warning.
            await Task.Delay(0);

            WriteLog("Transporter Room Panel Startup.");
            WriteLog("- Openening a channel.");
            _channel = new Channel("localhost:50051", ChannelCredentials.Insecure);

            // Optional: deadline.
            // Uncomment the delay in Server Program.cs to test this.
            // await _channel.ConnectAsync(deadline: DateTime.UtcNow.AddSeconds(2));

            WriteLog("- Channel open.");

            _client = new TransporterClient(_channel);
        }

        private void BeamUpOne_Click(object sender, RoutedEventArgs e)
        {
            var location = new Location
            {
                Description = Data.Locations.WhereEver()
            };

            var lifeForm = _client.BeamUp(location);

            WriteLog($"Beamed up {lifeForm.Rank} {lifeForm.Name} ({lifeForm.Species}) from {location.Description}.");
        }

        private async void BeamUpParty_Click(object sender, RoutedEventArgs e)
        {
            var location = new Location
            {
                Description = Data.Locations.WhereEver()
            };

            WriteLog($"Beaming up a party from {location.Description}.");

            using (var lifeForms = _client.BeamUpParty(location))
            {
                while (await lifeForms.ResponseStream.MoveNext())
                {
                    var lifeForm = lifeForms.ResponseStream.Current;
                    WriteLog($"- Beamed up {lifeForm.Rank} {lifeForm.Name} ({lifeForm.Species}).");
                }
            }

            WriteLog($"- Party beamed up.");
        }

        private async void BeamDownOne_Click(object sender, RoutedEventArgs e)
        {
            var whoEver = Data.LifeForms.WhoEver();
            var lifeForm = new LifeForm
            {
                Species = whoEver.Item1,
                Name = whoEver.Item2,
                Rank = whoEver.Item3
            };

            // var location = _client.BeamDown(lifeForm);
            // Uncomment the delay in the Service method to test the deadline.
            var location = await _client.BeamDownAsync(lifeForm, deadline: DateTime.UtcNow.AddSeconds(5));

            WriteLog($"Beamed down {lifeForm.Rank} {lifeForm.Name} ({lifeForm.Species}) to {location.Description}.");
        }

        private async void BeamDownParty_Click(object sender, RoutedEventArgs e)
        {
            var rnd = _rnd.Next(2, 5);
            var lifeForms = new List<LifeForm>();
            for (int i = 0; i < rnd; i++)
            {
                var whoEver = Data.LifeForms.WhoEver();
                var lifeForm = new LifeForm
                {
                    Species = whoEver.Item1,
                    Name = whoEver.Item2,
                    Rank = whoEver.Item3
                };

                lifeForms.Add(lifeForm);
            }

            WriteLog($"Beaming down a party.");

            using (var call = _client.BeamDownParty())
            {
                foreach (var lifeForm in lifeForms)
                {
                    await call.RequestStream.WriteAsync(lifeForm);
                    WriteLog($"- Beamed down {lifeForm.Rank} {lifeForm.Name} ({lifeForm.Species}).");
                }

                await call.RequestStream.CompleteAsync();

                var location = await call.ResponseAsync;

                WriteLog($"- Party beamed down to {location.Description}.");
            }
        }

        private async void ReplaceParty_Click(object sender, RoutedEventArgs e)
        {
            // Creating a party.
            var rnd = _rnd.Next(2, 5);
            var lifeForms = new List<LifeForm>();
            for (int i = 0; i < rnd; i++)
            {
                var whoEver = Data.LifeForms.WhoEver();
                var lifeForm = new LifeForm
                {
                    Species = whoEver.Item1,
                    Name = whoEver.Item2,
                    Rank = whoEver.Item3
                };

                lifeForms.Add(lifeForm);
            }

            WriteLog($"Replacing a party.");
            using (var call = _client.ReplaceParty())
            {
                var responseReaderTask = Task.Run(async () =>
                {
                    while (await call.ResponseStream.MoveNext())
                    {
                        var beamedDown = call.ResponseStream.Current;
                        await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            WriteLog($"- Beamed down {beamedDown.Rank} {beamedDown.Name} ({beamedDown.Species}).");
                        });
                    }
                });

                foreach (var request in lifeForms)
                {
                    await call.RequestStream.WriteAsync(request);
                    WriteLog($"- Beamed up {request.Rank} {request.Name} ({request.Species}).");
                };

                await call.RequestStream.CompleteAsync();

                await responseReaderTask;
            }

            WriteLog($"- Party replaced.");
        }

        private async void CloseChannel_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("Routing all energy to deflector shields.");
            await _channel.ShutdownAsync();
            WriteLog("- Transporter channel closed.");
        }

        private void WriteLog(string message)
        {
            Log.Text += message + " (stardate " + Stardate + ")" + Environment.NewLine;
            LogScroll.ChangeView(0, double.MaxValue, 1);
        }

        private string Stardate => DateTime.Now.ToString("mm:ss.fff");
    }
}
