# FTP-Uploader
FTP Uploader Component for JCJS Computing Project. Thanks to Steve Daniel for initial code

## Notes to tutor for assignment 1 submission 22/7/2018

This C# application will be installed on a Windows 10 tablet connected to the photobooth hardware. Its purpose is to automatically detect and upload new images taken by the hardware to the web component of this project, and add references to the uploaded images to the database.

### How to test
It is possible to test the functionality of the FTP uploader without having SocialBooth installed on your local machine. 

1.	Create a filepath on your local system called C:\SocialBooth\testevent\Originals
2.	Edit appSettings.txt for the ftpAddress, ftpUsername, sshHostKeyFingerprint, serverPath and connectionString to match the details of your locally installed SFTP server.
3.	Substitute booth.ppk (in the same directory as the executable) for a private key file with user privileges to write to the eventPhotos folder of the web application on the locally installed SFTP server. (The filename of this file must remain booth.ppk)

When ready to run the program:

1.	Run the program by double-clicking the FTPUploader.exe
2.	When prompted, enter ‘testevent’ for event name, 1 for the event ID and ‘n’ for unique codes.
3.	Once the program is running, you can paste a JPG image of resolution greater than 1660x1080 into the C:\SocialBooth\testevent\Originals folder and the program should detect and upload it.
4.	Finally, to verify the upload was successful, go to the web component and login with the event code for the event ID you entered. The image you pasted should be visible in the booth uploads section of the event gallery.



