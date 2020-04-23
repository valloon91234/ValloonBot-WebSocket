using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Bitmex.Client.Websocket.Client;
using Bitmex.Client.Websocket.Requests;
using Bitmex.Client.Websocket.Websockets;
using Serilog;
using Serilog.Events;

namespace Bitmex.Client.Websocket.Sample.NetFramework
{
    class Program111
    {
        private static readonly ManualResetEvent ExitEvent = new ManualResetEvent(false);

        private static readonly string API_KEY = "ZV8cXKP9JZL7SX9LNpV-8HaC";
        private static readonly string API_SECRET = "3yVBv241Fh7v6NP3GRVHq9wRnOby9P3GcFzO7WjQs7Tjmhsv";

        static void Main(string[] args)
        {
            InitLogging();

            AppDomain.CurrentDomain.ProcessExit += CurrentDomainOnProcessExit;
            Console.CancelKeyPress += ConsoleOnCancelKeyPress;

            Console.WriteLine("|=======================|");
            Console.WriteLine("|     BITMEX CLIENT     |");
            Console.WriteLine("|=======================|");
            Console.WriteLine();

            Log.Debug("====================================");
            Log.Debug("   STARTING (full .NET Framework)   ");
            Log.Debug("====================================");


            //var url = BitmexValues.ApiWebsocketUrl;
            var url = BitmexValues.ApiWebsocketTestnetUrl;
            using (var communicator = new BitmexWebsocketCommunicator(url))
            {
                using (var client = new BitmexWebsocketClient(communicator))
                {

                    client.Streams.InfoStream.Subscribe(info =>
                    {
                        Log.Information($"Reconnection happened, Message: {info.Info}, Version: {info.Version:D}");

                        client.Send(new PingRequest());
                        client.Send(new TradesSubscribeRequest("XBTUSD"));

                        if (!string.IsNullOrWhiteSpace(API_SECRET))
                            client.Send(new AuthenticationRequest(API_KEY, API_SECRET));
                    });

                    client.Streams.ErrorStream.Subscribe(x =>
                        Log.Warning($"Error received, message: {x.Error}, status: {x.Status}"));

                    client.Streams.AuthenticationStream.Subscribe(x =>
                    {
                        Log.Information($"Authentication happened, success: {x.Success}");
                        client.Send(new WalletSubscribeRequest());
                        client.Send(new MarginSubscribeRequest());
                        client.Send(new OrderSubscribeRequest());
                        client.Send(new PositionSubscribeRequest());
                    });

                    client.Streams.PongStream.Subscribe(x =>
                        Log.Information($"Pong received ({x.Message})"));


                    client.Streams.TradesStream.Subscribe(y =>
                       y.Data.ToList().ForEach(x =>
                           Log.Information($"Trade {x.Symbol} executed. Time: {x.Timestamp:mm:ss.fff}, Amount: {x.Size}, " +
                                           $"Price: {x.Price}, Direction: {x.TickDirection}"))
                        );

                    client.Streams.WalletStream.Subscribe(y =>
                       y.Data.ToList().ForEach(x =>
                           Log.Information($"-Wallet- {x.Amount} executed. Time: {x.Timestamp:mm:ss.fff}, Amount: {x.Account}, " +
                                           $"Price: {x.Addr}, Direction: {x.BalanceBtc}"))
                        );

                    client.Streams.MarginStream.Subscribe(y =>
                       y.Data.ToList().ForEach(x =>
                           Log.Information($"-Margin- {x.Amount} executed. Time: {x.Timestamp:mm:ss.fff}, Amount: {x.Account}, " +
                                           $"Price: {x.WalletBalance}, Direction: {x.MarginBalance}"))
                        );

                    client.Streams.OrderStream.Subscribe(y =>
                       y.Data.ToList().ForEach(x =>
                           Log.Information($"-Order- {x.AvgPx} executed. Time: {x.Timestamp:mm:ss.fff}, Amount: {x.Account}, " +
                                           $"Price: {x.CumQty}, Direction: {x.OrderQty}"))
                        );

                    communicator.Start();

                    ExitEvent.WaitOne();
                }
            }

            Log.Debug("====================================");
            Log.Debug("              STOPPING              ");
            Log.Debug("====================================");
            Log.CloseAndFlush();
        }

        private static void InitLogging()
        {
            var executingDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var logPath = Path.Combine(executingDir, "logs", "verbose.log");
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
                .WriteTo.ColoredConsole(LogEventLevel.Debug)
                .CreateLogger();
        }

        private static void CurrentDomainOnProcessExit(object sender, EventArgs eventArgs)
        {
            Log.Warning("Exiting process");
            ExitEvent.Set();
        }

        private static void ConsoleOnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Log.Warning("Canceling process");
            e.Cancel = true;
            ExitEvent.Set();
        }
    }
}
