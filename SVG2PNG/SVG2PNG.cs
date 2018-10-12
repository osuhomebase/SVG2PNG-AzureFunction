using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Drawing;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using System.Diagnostics;
using System;
using System.IO;
using System.Net.Http.Headers;

namespace SVG2PNG
{
    public static class SVG2PNG
    {
        [FunctionName("SVG2PNG")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous,
            "get", "post", Route = null)]HttpRequestMessage req, TraceWriter log, ExecutionContext context)
        {
            log.Info("C# HTTP trigger function processed a request.  We are alive!");

            // parse query parameter
            string svgURL = req.GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Compare(q.Key, "l", true) == 0)
                .Value;

            if (svgURL == null)
            {
                // Get request body
                dynamic data = req.Content.ReadAsAsync<object>();
                svgURL = data?.svgURL;
            }

            // download file from URL
            var uniqueName = GenerateId();
            Directory.CreateDirectory(Path.GetTempPath() + "\\" + uniqueName);
            try
            {
                using (var client = new WebClient())
                {
                    // storing in its own folder in temp storage to make it easier to find
                    client.DownloadFile(svgURL, Path.GetTempPath() + uniqueName + "\\" + uniqueName + ".svg");
                }
            }
            catch (Exception e)
            {
                log.Info("Download Fail");
                log.Info(e.Message);
            }

            Process proc = new Process();
            try
            {
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.CreateNoWindow = false;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.FileName = "java.exe";
                proc.StartInfo.Arguments = "-jar " + context.FunctionAppDirectory + "\\Batik\\batik-rasterizer.jar -d " + Path.GetTempPath() + uniqueName + "\\" + uniqueName + ".png " + Path.GetTempPath() + uniqueName + "\\" + uniqueName + ".svg";
                proc.Start();
                proc.WaitForExit();
                if (proc.HasExited)
                    log.Info(proc.StandardOutput.ReadToEnd());
                log.Info("Batik Success!");
            }
            catch (Exception e)
            {
                log.Info("Batik Fail");
                log.Info(Environment.GetEnvironmentVariable("JAVA_DIR") + "java.exe");
                log.Info(e.Message);
                // damn, no luck, better at least get rid of those images
                cleanup(uniqueName);
                //return new BadRequestResult();
            }

            try
            {
                log.Info(Path.GetTempPath() + uniqueName + ".png");
                //get Blob reference
                Image imageIn = Image.FromFile(Path.GetTempPath() + uniqueName + "\\" + uniqueName + ".png");


                // create an image response
                // thank you https://codehollow.com/2017/02/return-html-file-content-c-azure-function/
                var result = new HttpResponseMessage(HttpStatusCode.OK);
                result.Content = new ByteArrayContent(ImageToByteArray(imageIn));
                result.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");

                // close and clean up
                imageIn.Dispose();
                //cleanup(uniqueName);
                // optional, upload image to Azure Blob Storage
                //uploadImage(imageIn, uniqueName, log);

                return result;

            }
            catch (Exception e)
            {
                log.Info("Image Creation Fail");
                log.Info(e.Message);
                //cleanup(uniqueName);
            }

            // if we're still going, something went wrong.  Check your logs, man.
            return req.CreateResponse(HttpStatusCode.BadRequest, "Aww man, something went wrong!");
        }

        private static string GenerateId()
        {
            long i = 1;
            foreach (byte b in Guid.NewGuid().ToByteArray())
            {
                i *= ((int)b + 1);
            }
            return string.Format("{0:x}", i - DateTime.Now.Ticks);
        }

        private static byte[] ImageToByteArray(Image image)
        {
            // from this guy: https://blog.bitscry.com/2018/03/15/returning-image-files-from-an-azure-function/
            // note, this is the only thing I ended up using from him, but was a good tutorial that got me to current version
            ImageConverter converter = new ImageConverter();
            return (byte[])converter.ConvertTo(image, typeof(byte[]));
        }

        private static void cleanup(string uniqueName)
        {
            // clean up
            if (File.Exists(Path.GetTempPath() + "\\" + uniqueName + "\\" + uniqueName + ".png"))
                File.Delete(Path.GetTempPath() + "\\" + uniqueName + "\\" + uniqueName + ".png");
            if (File.Exists(Path.GetTempPath() + "\\" + uniqueName + "\\" + uniqueName + ".svg"))
                File.Delete(Path.GetTempPath() + "\\" + uniqueName + "\\" + uniqueName + ".svg");
            if (Directory.Exists(Path.GetTempPath() + "\\" + uniqueName))
                Directory.Delete(Path.GetTempPath() + "\\" + uniqueName);
        }

        private static bool uploadImage(Image imageIn, string uniqueName, TraceWriter log)
        {
            try
            {
                // upload file to blob storage
                string storageConnection = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
                CloudStorageAccount cloudStorageAccount = CloudStorageAccount.Parse(storageConnection);
                CloudBlobClient cloudBlobClient = cloudStorageAccount.CreateCloudBlobClient();
                //create a container CloudBlobContainer 
                //AzureWebJobsContainer is not created by default
                CloudBlobContainer cloudBlobContainer = cloudBlobClient.GetContainerReference(Environment.GetEnvironmentVariable("AzureWebJobsContainer"));

                byte[] arr;
                using (MemoryStream ms = new MemoryStream())
                {
                    imageIn.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                    arr = ms.ToArray();
                }

                CloudBlockBlob svgBlob = cloudBlobContainer.GetBlockBlobReference(Path.GetTempPath() + uniqueName + "\\" + uniqueName + ".png");
                svgBlob.Properties.ContentType = "image/png";
                svgBlob.UploadFromByteArray(arr, 0, arr.Length);
                return true;
            }
            catch (Exception e)
            {
                log.Info(e.Message);
                return false;
            }
        }

    }
}
