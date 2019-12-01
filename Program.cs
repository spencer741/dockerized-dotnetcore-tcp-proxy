//This program is a higher level, barebones implementation of a TCP Proxy server, using HTTP only.
//HTTPs implementation is not hard to implememt... although not in this code

//You can wrap the NetworkStream provided by TcpClient with an SslStream.This will provide the necessary handling of SSL and certificates.
//You may have to insert some handler code to perform a certificate validation callback, e.g.through ServicePointManager.ServerCertificateValidationCallback.
//https://docs.microsoft.com/en-us/dotnet/api/system.net.security.sslstream?redirectedfrom=MSDN&view=netframework-4.8

//https://docs.microsoft.com/en-us/dotnet/core/docker/build-container

//I have wrapped this .net core program in a linux docker container and deployed to an Azure Container Instance for cloud hosting...
//Here is a basic schematic on how to do all of the latter ... (This is very general and doesn't include some things like docker setup).
//But make sure docker is creating linux containers ... I chose linux to see how everything would work cross platform, and the fact that 
//Windows containers are a lot bulkier (in addition to the fact that they only support 1709 ... of course I was running 1903, so Windows
//containers would be more of a hassle in my circumstance).

// Open powershell to a directory of your choice...
// mkdir WINCONTAINER
// New-Item Dockerfile
// dotnet new console -o app -n myapp

//You need to populate the docker file and the program cs (different directories).

/* Dockerfile:
 * FROM mcr.microsoft.com/dotnet/core/runtime:3.0
 * COPY app/app/bin/Release/netcoreapp3.0/publish/ app/
 * ENTRYPOINT ["dotnet", "app/myapp.dll"]
 */

/* Program.cs
 * Paste TCP client code in here.
 */

//Rest of powershell commands...
//docker login
//docker build -t myimage -f Dockerfile .
//dotnet publish -c Release
// make sure publish directory exists ... somewhere around dir .\bin\release\netcoreapp3.0\
//docker tag myimage <dockerusername>/myimage:latest
//docker push <dockerusername>/myimage:latest

//Created by Spencer Arnold

//Some notes on the program itself
//This implementation could definitely be made more robust at handling TCP connections, especially on the memory handling side of things.
//However, this is a basic example that meets the requrements of getting my hands wet with C#, .netcore, docker, and Azure.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace NETCORE_TCP_PROXY
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Started...");
            //Create variable of type TcpListener, nulled out.
            TcpListener server = null;

            //Create variables for port and IP Address.
            //The address index will be different depending on network settings and machine.
            String strHostName = string.Empty;
            strHostName = Dns.GetHostName();
            IPHostEntry ipHostEntry = Dns.GetHostEntry(strHostName);
            IPAddress[] address = ipHostEntry.AddressList;
            Console.WriteLine("The Local IP Address: " + address[3].ToString());
            Int32 port = 13000;
            IPAddress Addr = IPAddress.Parse(address[3].ToString());

            //Create new TcpListener Object and set the appropriate IP and port to listen on
            //Done outside of try catch to terminate listener if catasrophe strikes.
            server = new TcpListener(Addr, port);

            Console.WriteLine(server.LocalEndpoint);

            try
            {

                #region ***The daemon listens for TCP connections on a specified port number.***

                // Start listening for client requests.
                server.Start();

                // Enter the listening loop.
                while (true)
                {
                    //Console.WriteLine("Waiting for TCP Connection");

                    /*Pending is a non-blocking method that determines if there are any pending connection requests. 
                     * Because the AcceptTcpClient method block execution until the 
                     * Start method has queued an incoming connection request, the Pending method can
                     * be used to determine if connections are available before attempting to accept them.*/
                    if (!server.Pending())
                    {

                        //Console.WriteLine("Sorry, no connection requests have arrived");

                    }
                    else
                    {

                        #region ***When a new client initiates a TCP connection request, the daemon accepts the request and establishes a new TCP connection with the new client.***

                        //The daemon spawns a new thread that is dedicated to handling the new client. 
                        //Below is a note on why creating another process is not feasible in this C# application.

                        /* "This is more a matter of .NET / CLR than of C#. Generally, it's a matter of the underlying operating system.
                         * Windows do not support fork()-like semantics of spawning new processes. 
                         * Also, fork() has nothing to do with multithreading support.
                         * The semantics of fork() involves duplicating the contents of the original process's address space.
                         * My opinion is this is an obsolete approach to process creation and has barely any room in the 
                         * Windows world, because it involves a lot of security and operating system architecture concerns.
                         * From the .NET point of view, the fundamental problem with fork() would be the approach to duplicating 
                         * and/or sharing unmanaged resources (file handles, synchronization objects, window handles (!), etc.) 
                         * between the old and the new process. I think there is no serious reason to introduce such concept either
                         * to .NET or to the underlying Windows operating system." - https://stackoverflow.com/questions/3913120/fork-concept-in-c-sharp
                         */

                        //In addition, I have decided to spawn threads as needed and not initialize a thread pool, 
                        //since theoretically any number of requests could hit the server given a width of time.

                        //Accept the pending client connection and return a TcpClient object initialized for communication.
                        TcpClient NewClient = server.AcceptTcpClient();

                        //ThreadStart TcpConnectionHandler = new ThreadStart(HandleTcpConnection);

                        //Compiler automatically selects proper delegate in Thread constructor ... no ThreadStart delegate needed
                        Thread thread = new Thread(HandleTcpConnection);

                        //Parameterized Thread entry point methods can only take type 'object' so internal casting has to be done.
                        thread.Start(NewClient);


                        #endregion

                    }

                    //Once thread has been spawned, the daemon process resumes listening for additional TCP connections from other clients.
                    //Continuing loop here.

                }

                #endregion
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
            }
            finally
            {
                // Stop listening for new clients.
                server.Stop();
            }


            Console.WriteLine("\nHit enter to continue...");
            Console.Read();

        }

       
        private static void HandleTcpConnection(object NewClient)
        {
            //This will be executed on another thread
            //Console.WriteLine("Connected! Spawned Thread {0}", Thread.CurrentThread.ManagedThreadId);

            //Set received tcp client for this thread.
            TcpClient Client = (TcpClient)NewClient;

            //Get Client stream object for reading and writing
            NetworkStream ClientStream = Client.GetStream();
            Client.ReceiveTimeout = 20000;
            Client.SendTimeout = 20000;
            //define target tcpclient and target stream (will get initialized once first read happens)
            TcpClient Target = null;
            NetworkStream TargetStream = null;
            
            //Client buffer variables for reading data
            byte[] bytesfromclient = new byte[8049];
            string datafromclient = String.Empty;

            //Target buffer variables for reading data
            byte[] bytesfromtarget = new byte[8049];
            string datafromtarget = String.Empty;

            //Store byte counts received for Client and Target
            int bytescli, bytestar;

            //Hostname
            string hostname;

            while (Client.Connected)
            {
                try
                {
                    if (ClientStream.DataAvailable || TargetStream == null)
                    {

                        bytescli = ClientStream.Read(bytesfromclient, 0, bytesfromclient.Length);

                        // Translate data bytes to a ASCII string.
                        datafromclient = System.Text.Encoding.ASCII.GetString(bytesfromclient, 0, bytesfromclient.Length);
                        //Console.WriteLine("Received data from Client:\n{0}", datafromclient);
                        //Get Hostname from incoming request
                        if (datafromclient.Contains("CONNECT") || datafromclient.Contains("GET") || datafromclient.Contains("POST"))
                        {
                            //parses out hostname from request.
                            hostname = GetHostName(datafromclient);
                            
                            //Trim hostname to insure parsing integrity.
                            hostname = hostname.Trim();

                            //establish new tcp client. 
                            //TcpClient resolves hostname internally, 
                            //port 80 is default for http on tcp
                            Target = new TcpClient(hostname, 80);
                            Target.ReceiveTimeout = 20000;
                            Target.SendTimeout = 20000;
                            TargetStream = Target.GetStream();

                            //Write bytes from Client to Target Stream
                            TargetStream.Write(bytesfromclient, 0, bytescli);

                            //Read bytes from Target
                            bytestar = TargetStream.Read(bytesfromtarget, 0, bytesfromtarget.Length);

                            //datafromtarget = System.Text.Encoding.ASCII.GetString(bytesfromtarget, 0, bytestar);

                            //Write bytes from target to client.
                            ClientStream.Write(bytesfromtarget, 0, bytestar);
                        }

                    }
                    else if (TargetStream.DataAvailable)
                    {

                        //Use bytestar count to only send back necessary information.
                        bytestar = TargetStream.Read(bytesfromtarget, 0, bytesfromtarget.Length);

                        datafromtarget = System.Text.Encoding.ASCII.GetString(bytesfromtarget, 0, bytestar);
                        //Console.WriteLine(datafromtarget);

                        ClientStream.Write(bytesfromtarget, 0, bytestar);
                    }
                    
                }
                catch (Exception e)
                {
                    Console.WriteLine("ERROR: " + e + "\n\n" + "Continuing . . .");
                    continue;
                }
            }

            Console.WriteLine("Cannot read! Closing all Connections.");

            //Force close streams;
            ClientStream.Close();
            TargetStream.Close();

            Client.Close();
            Target.Close();
        }

        //This function parses out and returns hostname from a request string
        private static string GetHostName(string datafromclient)
        {
            string hostname = null;
            int hostindex = datafromclient.IndexOf("Host: ") + 6;
            char c = datafromclient[hostindex];
            int n = 1;

            while (c != '\n')
            {
                // Console.WriteLine('!' + hostname + '!');
                hostname += c;
                c = datafromclient[hostindex + n];
                n++;
            }

            return hostname;

        }


        //Deprecated ... no longer used.
        //This function parses out and returns content length from a request string.
        private static int GetContentLength(string datafromclient)
        {
            string S_ContentLength = null;
            int ContentLengthIndex = datafromclient.IndexOf("Content-Length: ") + 16;
            char c = datafromclient[ContentLengthIndex];
            int n = 1;

            while (c != '\n')
            {
                S_ContentLength += c;
                c = datafromclient[ContentLengthIndex + n];
                n++;
            }

            return int.Parse(S_ContentLength);
        }
    }
}
