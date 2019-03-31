using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UnpackTurboCdPcePkg
{
    class Program
    {
        static void Main(string[] args)
        {
            //make sure proper number of aruments were passed
            if (args.Length != 1)
            {
                Console.WriteLine("Invalid arguments, only argument is the pce.pkg to unpack");
                return;
            }
            var filePath = args[0];

            //verify we can find the file
            if (!File.Exists(filePath))
            {
                Console.WriteLine("Can't find the specified file");
                return;
            }

            //get the pce.pkg as a byte array
            byte[] pkgData;
            using (var data = new MemoryStream())
            {
                using (var file = File.OpenRead(filePath))
                {
                    file.CopyTo(data);
                    pkgData = data.ToArray();
                }
            }

            //start the position tracker
            int position = 0; //variable to keep track of our position in the pkgData
            position = 4; //skip first four bytes that store the size in bytes of the rest of the file

            //get the bin file info
            var binSize = GetNextFileSize(pkgData, ref position);
            var binFileName = GetNextFileName(pkgData, ref position);
            var binFileArray = GetNextFileContents(pkgData, ref position, binSize);

            //get hcd file info
            var hcdSize = GetNextFileSize(pkgData, ref position);
            var nameAndDirectory = GetNextFileName(pkgData, ref position).Split('/');
            var outDirectory = nameAndDirectory[0];
            var hcdFileName = nameAndDirectory[1];
            var hcdFileArray = GetNextFileContents(pkgData, ref position, hcdSize);

            //create the output directory
            if (Directory.Exists(outDirectory))
            {
                Directory.Delete(outDirectory, true);
            }
            Directory.CreateDirectory(outDirectory);

            //create the pceconfig.bin and hcd files
            WriteFile($"{outDirectory}\\{binFileName}", binFileArray);
            WriteFile($"{outDirectory}\\{hcdFileName}", hcdFileArray);

            //loop over contents of the hcd file, and create the rest of the files
            string hcdFileText = File.ReadAllText($"{outDirectory}\\{hcdFileName}");
            var rows = hcdFileText.Split(new string[1] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < rows.Length; i++)
            {
                var columns = rows[i].Split(',');
                var fileName = columns[2];

                //get next files size
                var loopSize = GetNextFileSize(pkgData, ref position);

                //advance position over unneeded data
                position += outDirectory.Length; //skip the directory name
                position += 1; //skip the '/'
                position += fileName.Length; //skip the file name
                position += 1; //skip the null byte 

                //get next files contents
                var loopFileContentsArray = GetNextFileContents(pkgData, ref position, loopSize);

                //write file
                WriteFile($"{outDirectory}\\{fileName}", loopFileContentsArray);
            }
        }

        //get the size of the next file
        private static int GetNextFileSize(byte[] pkgData, ref int position)
        {
            var sizeArray = new byte[4];
            for (var i = 0; i < 4; i++)
            {
                sizeArray[i] = pkgData[position];
                position += 1;
            }
            return BitConverter.ToInt32(sizeArray, 0);
        }

        //get the file name of the next file
        private static string GetNextFileName(byte[] pkgData, ref int position)
        {
            //get the file name
            var fileNameArray = new List<byte>();
            for (int i = position; pkgData[i] != (byte)0; i++) //read until null byte
            {
                fileNameArray.Add(pkgData[i]);
                position += 1;
            }
            position += 1; //skip the null byte

            return Encoding.Default.GetString(fileNameArray.ToArray());
        }

        //get the contents of the next file
        private static byte[] GetNextFileContents(byte[] pkgData, ref int position, int fileSize)
        {
            var fileArray = new byte[fileSize];
            for (int i = 0; i < fileSize; i++)
            {
                fileArray[i] = pkgData[position];
                position += 1;
            }

            return fileArray;
        }

        //write file
        private static void WriteFile(string fileName, byte[] fileContents)
        {
            using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite))
            {
                fs.Write(fileContents, 0, fileContents.Length);
            }
        }
    }
}
