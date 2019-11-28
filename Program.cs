using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace NETCORE_TCP_PROXY
{
    class Program
    {

        static void Main(string[] args)
        {

            //Create variable of type TcpListener, nulled out.
            TcpListener server = null;

            //Create variables for port and IP Address.
            Int32 port = 13000;
            IPAddress localAddr = IPAddress.Parse("127.0.0.1");

            //Create new TcpListener Object and set the appropriate IP and port to listen on
            //Done outside of try catch to terminate listener if catasrophe strikes.
            server = new TcpListener(localAddr, port);

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

        /*
         * TODO: This is the last buildout that needs to happen... 
         * 
            The thread establishes a new TCP connection to the targeted server. 
            (Extracting Hostname from beginnging of HTTP message, resolving to IP address)

            The thread falls into a loop in which it acts as an intermediator exchanging 
            data(reading / writing <or> writing / reading) between the client and the targeted server.
             
             
        */
        private static void HandleTcpConnection(object NewClient)
        {
            //This will be executed on another thread

            TcpClient Client = (TcpClient) NewClient;


            Console.WriteLine("Connected! Spawned Thread {0}", Thread.CurrentThread.ManagedThreadId);

            //Buffer variables for reading data
            byte[] bytes = new byte[1000];
            string data = null;


            // Get a stream object for reading and writing
            NetworkStream stream = Client.GetStream();

            int i;

            // Loop to receive all the data sent by the client.
            while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
            {
                // Translate data bytes to a ASCII string.
                data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);


                //Just for fun. You don't believe it til you see it. GET' ' Really does send a space. 
                for (int h = 0; h < bytes.Length; h++)
                    Console.WriteLine("Byte " + h + " : " + Convert.ToString(bytes[h], 2) + " Char : " + bytes[h].ToString() + '\n');

                //Console.WriteLine("Received: {0}", data);

                // Process the data sent by the client.
                data = data.ToUpper();

                byte[] msg = System.Text.Encoding.ASCII.GetBytes(data);

                // Send back a response.
                //stream.Write(msg, 0, msg.Length);
                //Console.WriteLine("Sent: {0}", data);
            }

            // Shutdown and end connection
            Client.Close();

        }

    }
}