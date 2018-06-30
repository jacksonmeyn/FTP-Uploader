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
        public static string serverPath;
        public static string remoteDirectory;

        static void Main(string[] args)
        {

            List<String> localSubdirectories = new List<string>();
            List<FileSystemWatcher> fileSystemWatchers = new List<FileSystemWatcher>();

            string welcomeMessage = "Welcome to the SocialBooth FTP Uploader V1.0 designed for Little Red Photobooth";
            Console.WriteLine(welcomeMessage.ToUpper());
            Console.WriteLine("===========================================================================");
            

            Console.WriteLine("Attempting to open settings file at {0}...", Directory.GetCurrentDirectory() + @"\appSettings.txt");
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
                            foreach (XmlNode subfolderNode in subfolders.ChildNodes)
                            {
                                localSubdirectories.Add(localRootFolder + subfolderNode.InnerText);
                            }
                            break;
                        case "ServerPath":
                            serverPath = node.InnerText;
                            break;
                    }

                  
                }

                Console.WriteLine("Settings file opened successfully");
            }
            catch (Exception e)
            {
                Console.WriteLine("Error opening setting file: " + e.ToString(), 0);
                Console.ReadLine();
            }

            //Prompt user for event ID an check it exists on server
            bool eventExists = false;
            while (!eventExists)
            {
                Console.WriteLine("Please enter the ID number of the event you wish to upload this session's photos to");
                try
                {
                    int eventID = Convert.ToInt32(Console.ReadLine());
                    Session testSession = new Session();
                    // Set up session options
                    SessionOptions testSessionOptions = new SessionOptions
                    {
                        Protocol = Protocol.Sftp,
                        HostName = ftpAddress,
                        UserName = ftpUsername,
                        SshHostKeyFingerprint = sshHostKeyFingerprint,
                        SshPrivateKeyPath = Directory.GetCurrentDirectory() + @"\booth.ppk",
                    };

                    // Connect
                    testSession.Open(testSessionOptions);

                    // Your code
                    remoteDirectory = serverPath + Convert.ToString(eventID);
                    eventExists = testSession.FileExists(remoteDirectory);
                    if (!eventExists)
                    {
                        Console.WriteLine("We can't seem to find that event on the server. Please check it exists and try again.");
                    } else
                    {
                        Console.WriteLine("Thanks!");
                    }
                }
                catch
                {
                    Console.WriteLine("The event ID must be a number. Try again.");
                }
            }

            //Process preexisting files
            Console.WriteLine("Checking for existing files...");

            //// Monitors directory for changes
            foreach (string filepath in localSubdirectories)
            {
                Console.WriteLine("Checking for existing files in {0}...", filepath);
                //Create FileSystemWatcher
                FileSystemWatcher fsw = new FileSystemWatcher(filepath);
           
                //Process preexisting files
                string[] existingFiles = Directory.GetFiles(filepath);
                foreach (string file in existingFiles)
                {
                    Console.WriteLine("File {0} found", Path.GetFileName(file));
                    ProcessFile(Path.GetFileName(file), fsw);
                }
                Console.WriteLine("Processing existing files in {0} complete", filepath);
                Console.WriteLine("=====================================================");

                //Add watcher for new files
                fileSystemWatchers.Add(fsw);
            }
            watch();
            Console.ReadKey();

            // Call method to monitor directory
            void watch()
            {
                Console.WriteLine("Commence Monitoring these folder(s) for new files:");
                foreach (string fp in localSubdirectories)
                {
                    Console.WriteLine(fp);
                }
                Console.WriteLine("=====================================================");

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
                Console.WriteLine("New image detected at {0}", e.FullPath);
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

                    try
                    {
                        Thread.Sleep(1000); // wait 1 second to ensure the file has fully copied

                        //GIFs take longer for SocialBooth to create, so wait a little longer
                        if (Path.GetExtension(originalFile) == ".gif")
                        {
                            Thread.Sleep(6000);
                        }
                        
                        Console.WriteLine("Opening File: " + originalFile);

                        //Move image to subdirectory
                        Console.Write("Attempting to move file {0} to {1}...", originalFile, movedOriginal);
                        File.Move(originalFile, movedOriginal);
                        Console.WriteLine("completed");

                    // Open file stream
                    Console.WriteLine("Opening image {0}...", movedOriginal);
                        Stream s = File.Open(movedOriginal, FileMode.Open);
                        Image originalImageObject = Image.FromStream(s);
                        Console.WriteLine("File Stream Opened: " + movedOriginal);

                    //Prepare to resize image
                    Console.WriteLine("Resizing image {0}...", movedOriginal);
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
                        Console.WriteLine("Resized Image {0} and moved to {1} successfully", fileName, ftpQueuePath + fileName);

                        // Dispose of objects before deleting old file
                        resizedImageObject.Dispose();
                        originalImageObject.Dispose();
                        s.Close();

                        // Set objects as null for garbage collection
                        originalImageObject = null;
                        resizedImageObject = null;
                        s = null;

                        // Call ftp upload method
                        FTPImageUpload(fileName, ftpQueuePath);
                    }
                    catch (IOException ex)
                    {
                        Console.WriteLine(ex); // Write error
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
            
            Console.WriteLine("Connecting to FTP Server..."); // Success
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
                    session.Open(sessionOptions);

                    // Your code
                    Console.Write("Attempting upload of {0}...", ftpQueuePath + currentFilename);
                    var transferResult = session.PutFiles(ftpQueuePath + currentFilename, remoteDirectory, false);
                    transferResult.Check();
                    Console.WriteLine("done");
                    //// Delete source file
                    Console.Write("Deleting {0} from queue...", currentFilename); // Success
                    File.Delete(ftpQueuePath + currentFilename);
                    Console.WriteLine("done");
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine(ex); // Write error
            }
        }
    }
}
