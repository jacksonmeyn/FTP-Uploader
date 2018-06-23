using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Threading;
using System.Net;
using System.Xml;
using System.Collections.Generic;
using WinSCP;

namespace FTPUploader
{
    class Program
    {

        public static string ftpAddress;
        public static string localRootFolder;
        public static string ftpUsername;
        public static string sshHostKeyFingerprint;

        static void Main(string[] args)
        {

            List<String> localSubdirectories = new List<string>();
            List<FileSystemWatcher> fileSystemWatchers = new List<FileSystemWatcher>();

            try
            {
                //// Open xml settings file
                XmlDocument XMLSettings = new XmlDocument();
                XMLSettings.Load(Directory.GetCurrentDirectory() + @"\appSettings.txt");

                // cycle through each child node in settings file to get values
                foreach (XmlNode node in XMLSettings.DocumentElement.ChildNodes)
                {
                    switch (node.Name)
                    {
                        case "localRootFolder":
                            localRootFolder = node.InnerText;
                            break;
                        case "ftpAddress":
                            ftpAddress = node.InnerText;
                            break;
                        case "ftpUsername":
                            ftpUsername = node.InnerText;
                            break;
                        case "sshHostKeyFingerprint":
                            sshHostKeyFingerprint = node.InnerText;
                            break;
                        case "localSubfolders":
                            XmlNode subfolders = node;
                            Console.WriteLine(subfolders.Name);
                            foreach (XmlNode subfolderNode in subfolders.ChildNodes)
                            {
                                Console.WriteLine(subfolderNode.InnerText);
                                localSubdirectories.Add(localRootFolder + subfolderNode.InnerText);
                                Console.WriteLine(localRootFolder + subfolderNode.InnerText);
                            }
                            break;
                    }
                }

            }
            catch (Exception e)
            {
                Console.WriteLine("Error opening setting file: " + e.ToString(), 0);
                Console.ReadLine();
            }

            //Process preexisting files
            Console.WriteLine("Processing existing files...");

            //// Monitors directory for changes
            foreach (string filepath in localSubdirectories)
            {
                //Create FileSystemWatcher
                FileSystemWatcher fsw = new FileSystemWatcher(filepath);
           
                //Process preexisting files
                string[] existingFiles = Directory.GetFiles(filepath);
                foreach (string file in existingFiles)
                {
                    Console.WriteLine("debug: " + Path.GetFileName(file));
                    ProcessFile(Path.GetFileName(file), fsw);
                }
                Console.WriteLine("Processing existing files for {0} complete", filepath);

                //Add watcher for new files
                fileSystemWatchers.Add(fsw);
            }
            watch();
            Console.ReadKey();

            // Call method to monitor directory
            void watch()
            {
                Console.WriteLine("*************************************");
                Console.WriteLine("Monitoring folder(s) for new files");
                foreach (string fp in localSubdirectories)
                {
                    Console.WriteLine(fp);
                }
                Console.WriteLine("*************************************");

                foreach (FileSystemWatcher f in fileSystemWatchers)
                {
                    f.Created += new FileSystemEventHandler(OnChanged);
                    f.EnableRaisingEvents = true;
                    f.IncludeSubdirectories = false;
                    if (f.Path.Contains("GIF"))
                    {
                        f.Filter = "*.gif";
                    }
                    
                }
                
            }

            // When file created call method
            void OnChanged(object source, FileSystemEventArgs e)
            {
                Console.WriteLine("debug: " + e.Name);
                ProcessFile(e.Name, (FileSystemWatcher)source);
            }
        }

        /////////////////////////////////////////////////
        // Method to process files created in directory
        /////////////////////////////////////////////////
        public static void ProcessFile (string fileName, FileSystemWatcher fsw)
        {
            //Create 'processed' directory to move originals into
            string processedDirectory = fsw.Path + @"processed\";
            Directory.CreateDirectory(processedDirectory);

            //Create FTP Queue folder to move resized originals into for uploadd
            string ftpQueuePath = localRootFolder + @"FTPQueue\";
            Directory.CreateDirectory(ftpQueuePath);

            string originalFile = fsw.Path + fileName;
            string movedOriginal = processedDirectory + fileName;
            string resizedFile = ftpQueuePath + fileName;

            if (".jpg.jpeg.gif".Contains(Path.GetExtension(originalFile)))
            {
                bool fileMoved = false;
                while (!fileMoved)
                {
                    try
                    {
                        Thread.Sleep(1000); // wait 1 second to ensure the file has fully copied

                        //GIFs take longer for SocialBooth to create, so wait a little longer
                        if (Path.GetExtension(originalFile) == ".gif")
                        {
                            Console.WriteLine("File is GIF, waiting longer");
                            Thread.Sleep(6000);
                        }
                        
                        Console.WriteLine("Opening New File: " + originalFile);

                        //Move image to subdirectory
                        Console.WriteLine("Attempting to move file...");
                        File.Move(originalFile, movedOriginal);
                        fileMoved = true;

                        // Open file stream
                        Stream s = File.Open(movedOriginal, FileMode.Open);
                        Image originalImageObject = Image.FromStream(s);
                        Console.WriteLine("File Stream Opened: " + movedOriginal);

                        //Prepare to resize image
                        Image resizedImageObject;

                        // If image is original photo, resize to 1024 x 768 pixels, otherwise don't resize
                        if (fsw.Filter == "*.gif" || (originalImageObject.Width < 900 || originalImageObject.Width > 1100))
                        {
                            resizedImageObject = originalImageObject;
                        }
                        else
                        {
                            resizedImageObject = ResizeImage(originalImageObject, new Size(1024, 720));
                        }


                        // Rename file to unique filename
                        //string newFilename = "BoothPhoto_" + DateTime.Now.Ticks + ".jpg";

                        ImageFormat format;
                        // Save to upload queue directory
                        if (fsw.Filter != "*.gif")
                        {
                            format = ImageFormat.Jpeg;
                        }
                        else
                        {
                            format = ImageFormat.Gif;
                        }
                        resizedImageObject.Save(ftpQueuePath + fileName, format);
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
                        //File.Delete(originalFile);
                        //Console.WriteLine("Original File Deleted - " + originalFile);

                        // Call ftp upload method
                        FTPImageUpload(fileName, ftpQueuePath);
                    }
                    catch (IOException ex)
                    {
                        Console.WriteLine(ex); // Write error
                    }
                }
                
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
                using (Session session = new Session())
                {
                    // Set up session options
                    SessionOptions sessionOptions = new SessionOptions
                    {
                        Protocol = Protocol.Sftp,
                        HostName = ftpAddress,
                        UserName = ftpUsername,
                        SshHostKeyFingerprint = sshHostKeyFingerprint,
                        SshPrivateKeyPath = Directory.GetCurrentDirectory() + @"\booth.ppk",
                    };

                    // Connect
                    Console.WriteLine("Attempting open");
                    session.Open(sessionOptions);

                    // Your code
                    Console.WriteLine("Attempting upload");
                    var transferResult = session.PutFiles(ftpQueuePath + currentFilename, "/opt/bitnami/apache2/htdocs/", false);
                    transferResult.Check();
                    Console.WriteLine("done");
                    //// Delete source file
                    Console.WriteLine("Deleting local file from queue"); // Success
                    File.Delete(ftpQueuePath + currentFilename);
                }
                //// Set FTP server credentials
                //WebClient client = new WebClient
                //{
                //    Credentials = new NetworkCredential(ftpUsername, ftpPassword)
                //};
                //Console.WriteLine("Uploading..."); // Success

                //// FTP upload using details in settings file
                //client.UploadFile(ftpAddress + currentFilename, ftpQueuePath + currentFilename);
                //Console.WriteLine("Finished"); // Success

                //// Delete source file
                //Console.WriteLine("Deleting local file from queue"); // Success
                //File.Delete(ftpQueuePath + currentFilename); // Try to move
                //Console.WriteLine("Finished"); // Success
            }
            catch (IOException ex)
            {
                Console.WriteLine(ex); // Write error
            }
        }
    }
}
