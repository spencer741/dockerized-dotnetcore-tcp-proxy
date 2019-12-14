# NETCORE_TCP_PROXY

## Program Documentation

### General Notes
This program is a higher level barebones implementation of a TCP Proxy server, using HTTP only. I wrote and deployed this in a few hours, so open any issues for improvements / bug fixes we can make :)

This is written in C# utilizing .net core.

Since Microsoft provided a class around the socket layer (named “TcpClient” along with “NetworkStream”), they were used to simplify a standard sockets implementation.

In the code, the daemon spawns a new thread that is dedicated to handling the new client, not a new process.

### Program Deployment
I have wrapped this .net core program in a Linux docker container and deployed to an Azure Container Instance for cloud hosting.
I decided to create a tutorial style rundown on how i've deployed the Tcp Proxy code written.
Here is a basic schematic on how to do all of the latter ... (This is very general and doesn't include some things like docker setup), but make sure docker is creating Linux containers. 
I chose Linux to see how everything would work cross platform, and the fact that Windows containers are a lot bulkier (in addition to the fact that they only support 1709 ... Of course I was running 1903, so Windows containers would be more of a hassle in my circumstance).
Open PowerShell to a directory of your choice...
```
mkdir WINCONTAINER
New-Item Dockerfile
dotnet new console -o app -n myapp
```

You need to populate the “Dockerfile” and the “Program.cs” files with correct code (they will be in different directories)
Dockerfile:
```
 FROM mcr.microsoft.com/dotnet/core/runtime:3.0
 COPY app/app/bin/Release/netcoreapp3.0/publish/ app/
 ENTRYPOINT ["dotnet", "app/myapp.dll"]
```

Program.cs:
```
Paste TCP client code in here.
```


Here are the rest of the powershell commands…
```
dotnet publish -c Release
make sure publish directory exists ... somewhere around dir .\bin\release\netcoreapp3.0\
docker login
docker build -t myimage -f Dockerfile .
docker tag myimage <dockerusername>/myimage:latest
docker push <dockerusername>/myimage:latest
```
From there, I referred to the image on dockerhub through the setup screen when creating a Container instance in Azure.
I then initialized a port in the Azure setup screens for the container instance [13000, TCP] ... is what my program listens on.

If you want to run the program you can navigate to release. If you make any mods to release, build for release.
Navigate to:
```
bin\Release\netcoreapp3.0\myapp.exe and run the executable.
```
OR Just
```
dotnet run
``` 
Note:
It will get the 3rd IP address in the address listing entries and listen on port 13000. 
If you would like to provide it your own IP and Port to listen on, or if it is not detecting the proper IP address, simply alter the code in line 32, with the Schema. 
```
//TcpClient(String, Int32) for Ipv4 address and Port.
server = new TcpListener(Addr, port);
```

### Some other notes on the program itself
This implementation could definitely be made more robust at handling TCP connections, especially on the memory handling side of things.
However, this is a basic example that meets the requirements with C#, .netcore, docker, and Azure.

Open an issue for any questions or fixes if you are using this as a reference.

**-Spencer**
