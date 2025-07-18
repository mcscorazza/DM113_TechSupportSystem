using Microsoft.AspNetCore.Server.Kestrel.Core;
using DM113_TechSupport.Server.Services;
using System.Text;

namespace DM113_TechSupport.Server
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // gRPC exige HTTP/2
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenLocalhost(5000, o => o.Protocols = HttpProtocols.Http2);
            });

            builder.Services.AddGrpc();

            var app = builder.Build();
            app.MapGrpcService<SupportService>();
            //Console.OutputEncoding = Encoding.UTF8;
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine("Servidor gRPC rodando em http://localhost:5000");
            Console.WriteLine("\nSistema de Suporte Técnico | SERVIDOR\n");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.DarkBlue;
            Console.WriteLine("\n");
            Console.WriteLine("  ╔════════════════════════════════════════╗");
            Console.WriteLine("  ║                  SISUTÉ                ║");
            Console.WriteLine("  ╠════════════════════════════════════════╣");
            Console.WriteLine("  ║                                        ║");
            Console.WriteLine("  ║       ███████╗ ███████╗ ████████╗      ║");
            Console.WriteLine("  ║       ██╔════╝ ██╔════╝ ╚══██╔══╝      ║");
            Console.WriteLine("  ║       ███████╗ ███████╗    ██║         ║");
            Console.WriteLine("  ║       ╚════██║ ╚════██║    ██║         ║");
            Console.WriteLine("  ║       ███████║ ███████║    ██║         ║");
            Console.WriteLine("  ║       ╚══════╝ ╚══════╝    ╚═╝         ║");
            Console.WriteLine("  ║                                        ║");
            Console.WriteLine("  ╚════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("\n");
            
            app.Run();
        }
    }
}