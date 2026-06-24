using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace IdeaCadConnector.OcrTool
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Usage: OcrTool.exe <image-path>");
                return 1;
            }

            var imagePath = args[0];
            if (!File.Exists(imagePath))
            {
                Console.Error.WriteLine($"File not found: {imagePath}");
                return 1;
            }

            try
            {
                var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(imagePath));
                using (var stream = await file.OpenReadAsync())
                {
                    var decoder = await BitmapDecoder.CreateAsync(stream);
                    var bitmap = await decoder.GetSoftwareBitmapAsync();

                    var engine = OcrEngine.TryCreateFromUserProfileLanguages();
                    if (engine == null)
                    {
                        engine = OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"));
                    }
                    if (engine == null)
                    {
                        Console.Error.WriteLine("No OCR engine available.");
                        return 1;
                    }

                    var result = await engine.RecognizeAsync(bitmap);
                    if (string.IsNullOrWhiteSpace(result.Text))
                    {
                        Console.WriteLine("(no text detected)");
                    }
                    else
                    {
                        Console.WriteLine(result.Text);
                    }
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR: {ex.Message}");
                return 1;
            }
        }
    }
}
