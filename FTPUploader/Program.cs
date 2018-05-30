using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Threading;
using System.Net;
using System.Xml;

namespace FTPUploader
{
    class Program
    {
        public static string ftpAddress;
        public static string localFolder;
        public static string ftpUsername;
        public static string ftpPassword;

        static void Main(string[] args)
        {
            try
            {
                // Open xml settings file
                XmlDocument XMLSettings = new XmlDocument();
                XMLSettings.Load(Directory.GetCurrentDirectory() + @"\appSettings.txt");

                // cycle through each child node in settings file to get values
                foreach (XmlNode node in XMLSettings.DocumentElement.ChildNodes)
                {
                    switch (node.Name)
                    {
                        case "localFolder":
                            localFolder = node.InnerText;
                            break;
                        case "ftpAddress":
                            ftpAddress = node.InnerText;
                            break;
                        case "ftpUsername":
                            ftpUsername = node.InnerText;
                            break;
                        case "ftpPassword":
                            ftpPassword = node.InnerText;
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error opening setting file: " + e.ToString(), 0);
            }

            // Monitors directory for changes
            FileSystemWatcher watcher = new FileSystemWatcher();
            watch();
            Console.ReadKey();

            // Call method to monitor directory
            void watch()
            {
                Console.WriteLine("*************************************");
                Console.WriteLine("Monitoring folder for new images");
                Console.WriteLine("*************************************");

                watcher.Path = localFolder;
                // only watch for jpg files in directory
                watcher.Filter = "*.jpg";
                watcher.Created += new FileSystemEventHandler(OnChanged);
                watcher.EnableRaisingEvents = true;
            }

            // When file created call method
            void OnChanged(object source, FileSystemEventArgs e)
            {
                ProcessFile(e.Name);    
            }
        }

        /////////////////////////////////////////////////
        // Method to process files created in directory
        /////////////////////////////////////////////////
        public static void ProcessFile (string fileName)
        {
            string ftpQueuePath = localFolder + @"FTPQueue\";
            string originalFile = localFolder + fileName;
            string resizedFile = ftpQueuePath + fileName;

            // Check file extension
            string extension = Path.GetExtension(originalFile);

            if (extension == ".jpg")
            {
                try
                {
                    Thread.Sleep(1000); // wait 1 second to ensure the file has fully copied
                    Console.WriteLine("Opening New File: " + originalFile);

                    // Open file stream
                    Stream s = File.Open(originalFile, FileMode.Open);
                    Image originalImageObject = Image.FromStream(s);
                    Console.WriteLine("File Stream Opened: " + originalFile);

                    // Resize image to 1024 x 768 pixels
                    Image resizedImageObject = ResizeImage(originalImageObject, new Size(1024, 768));

                    // Rename file to unique filename
                    string newFilename = "BoothPhoto_" + DateTime.Now.Ticks + ".jpg";

                    // Save to upload queue directory
                    resizedImageObject.Save(ftpQueuePath + newFilename, ImageFormat.Jpeg);
                    Console.WriteLine("Resized Photo Successfully");

                    // Dispose of objects before deleting old file
                    resizedImageObject.Dispose();
                    originalImageObject.Dispose();
                    s.Close();
                    Console.WriteLine("Objects Disposed");

                    // Set objects as null for garbage collection
                    originalImageObject = null;
                    resizedImageObject = null;
                    s = null;
                    Console.WriteLine("Objects Set as Null");

                    // Delete full size original image
                    File.Delete(originalFile);
                    Console.WriteLine("Original File Deleted - " + originalFile);

                    // Call ftp upload method
                    FTPImageUpload(newFilename, ftpQueuePath);
                }
                catch (IOException ex)
                {
                    Console.WriteLine(ex); // Write error
                }
            } else
            {
                File.Delete(originalFile); // delete invalid file
                Console.WriteLine("Invalid File Deleted - " + originalFile); // Success
            }
        }

        /////////////////////////////////////////////////
        // Method to resize the image for faster uploads
        /////////////////////////////////////////////////
        public static Image ResizeImage(Image image, Size size)
        {
            int newWidth;
            int newHeight;

            // Preserver original aspect ratio when resizing image
            int originalWidth = image.Width;
            int originalHeight = image.Height;
            float percentWidth = (float)size.Width / (float)originalWidth;
            float percentHeight = (float)size.Height / (float)originalHeight;
            float percent = percentHeight < percentWidth ? percentHeight : percentWidth;
            newWidth = (int)(originalWidth * percent);
            newHeight = (int)(originalHeight * percent);

            // Create a new image at new size
            Image newImage = new Bitmap(newWidth, newHeight);
            using (Graphics graphicsHandle = Graphics.FromImage(newImage))
            {
                // Specify quality settings
                graphicsHandle.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphicsHandle.DrawImage(image, 0, 0, newWidth, newHeight);
            }
            return newImage;
        }

        /////////////////////////////////////////////////
        // Mehtod to upload file to FTP server
        /////////////////////////////////////////////////
        public static void FTPImageUpload(string currentFilename, string ftpQueuePath)
        {
            Console.WriteLine("Connecting to FTP Server"); // Success
            try
            {
                // Set FTP server credentials
                WebClient client = new WebClient
                {
                    Credentials = new NetworkCredential(ftpUsername, ftpPassword)
                };
                Console.WriteLine("Uploading..."); // Success

                // FTP upload using details in settings file
                client.UploadFile(ftpAddress + currentFilename, ftpQueuePath + currentFilename);
                Console.WriteLine("Finished"); // Success

                // Delete source file
                Console.WriteLine("Deleting local file from queue"); // Success
                File.Delete(ftpQueuePath + currentFilename); // Try to move
                Console.WriteLine("Finished"); // Success
            }
            catch (IOException ex)
            {
                Console.WriteLine(ex); // Write error
            }
        }
    }
}
