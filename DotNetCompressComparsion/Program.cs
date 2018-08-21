using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DotNetCompressComparsion
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var urls = new[]
            {
                "https://blogs.msdn.microsoft.com/dotnet/2017/07/27/introducing-support-for-brotli-compression/", // HTML
                "https://www.reddit.com/",
                "http://kmmc.in/wp-content/uploads/2014/01/lesson2.pdf", // PDF
                "https://www.dcu.ie/sites/default/files/students_learning/docs/WC_Numbers-in-academic-writing.pdf",
                "https://weeknumber.net/calendar/united-states/2018.xlsx", // XLSX
                "https://www.lloyds.com/~/media/files/the-market/business-timetable/capacity/2018/2018-list-of-syndicates.xlsx?la=en",
                "http://i.imgur.com/wQuZpVK.jpg", // JPG
                "https://i.redditmedia.com/fpBIIKKGAtS2UPUU82e8M2CUcT7ryY3g7WcfAEgmt3M.jpg?fit=crop&crop=faces%2Centropy&arh=2&w=640&s=2c34d910aa80882d074974a287774cd3",
                "https://lh3.ggpht.com/muLFmvz_MMPBv4-LU5DA7sJl6lvKSI6mWYb6Vwyyj7z7MOKFHlbgV8WlZz_nRpxHjA=h310", // PNG
                "http://www.pngpix.com/wp-content/uploads/2016/10/PNGPIX-COM-Zombie-Dog-PNG-Transparent-Image.png"
            };

            var client = new HttpClient();

            foreach (var url in urls)
            {
                var response = await client.GetAsync(url);
                Console.WriteLine($"URL: {url}\nContent-Type: {response.Content.Headers.ContentType}");

                var input = await response.Content.ReadAsStreamAsync();
                var result = new List<CompressResult>
                {
                    new CompressResult { Name = "Original", Length = input.Length },
                    await CompressWith<GZipStream>(input),
                    await CompressWith<DeflateStream>(input),
                    await CompressWith<Brotli.BrotliStream>(input, x =>
                    {
                        //x.SetLength(3);  // not implemented
                        x.SetWindow(18);
                    }),
                    await CompressWith<BrotliSharpLib.BrotliStream>(input, x =>
                    {
                        x.SetQuality(3);
                        x.SetWindow(18);
                    }),
                };

                var ranking = result.OrderBy(x => x.Length == 0 ? long.MaxValue : x.Length).ThenBy(x => x.ElapsedTicks);

                foreach (var record in ranking)
                {
                    var ratio = record.CompressRatio.HasValue ? $"({Math.Round(record.CompressRatio.Value * 100, 2)}%)" : "";
                    Console.WriteLine($"{record.Name}\n\tlen: {record.Length} {ratio}\n\tms: {record.ElapsedMilliseconds}");
                }
                Console.WriteLine("");
            }

            Console.ReadKey();
        }

        static async Task<CompressResult> CompressWith<TAlgorithm>(Stream source, Action<TAlgorithm> arranger = null) where TAlgorithm : class
        {
            using (var output = new MemoryStream())
            using (var compressOutput = (Stream)Activator.CreateInstance(typeof(TAlgorithm), output, CompressionMode.Compress))
            {
                arranger?.Invoke(compressOutput as TAlgorithm);

                source.Seek(0, SeekOrigin.Begin);

                var stopWatch = new Stopwatch();
                stopWatch.Start();
                await source.CopyToAsync(compressOutput);
                stopWatch.Stop();

                return new CompressResult
                {
                    Name = typeof(TAlgorithm).FullName,
                    Length = output.Length,
                    CompressRatio = output.Length / (double)source.Length,
                    ElapsedMilliseconds = stopWatch.ElapsedMilliseconds,
                    ElapsedTicks = stopWatch.ElapsedTicks
                };
            }
        }
    }

    public class CompressResult
    {
        public string Name { get; set; }
        public long Length { get; set; }
        public double? CompressRatio { get; set; }
        public long? ElapsedMilliseconds { get; set; }
        public long? ElapsedTicks { get; set; }
    }
}
