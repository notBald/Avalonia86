\# Build Environment Setup Guide: Avalonia86



Guide for setting up a Linux Mint VM to build \*\*Avalonia86\*\* targeting \*\*.NET 10 (X and Wayland)\*\* and \*\*.NET 6 (Windows 7 - 11)\*\*.



\## 1. Prerequisites \& Downloads

Download the following software to your host machine:

\*   \*\*VirtualBox:\*\* \[virtualbox.org/wiki/Downloads](https://www.virtualbox.org/wiki/Downloads)

\*   \*\*Linux Mint (Cinnamon Edition):\*\* \[linuxmint.com/download.php](https://www.linuxmint.com/download.php)



You can use other distributions of Linux, but this guide assumes Mint.



\## 2. Virtual Machine Configuration

Create a new VM in VirtualBox. These settings work:

\*   \*\*OS Type:\*\* Linux / Ubuntu (64-bit)

\*   \*\*CPU:\*\* 1 Core

\*   \*\*RAM:\*\* 8 GB

\*   \*\*Disk:\*\* 50 GB VDI (Dynamically allocated)

\*   \*\*Graphics:\*\* VMSVGA with 16MB Video Memory and 3D Acceleration enabled



\## 3. Install Linux Mint in the VM

\*  Insert the CD Image in the virtual CD drive and follow on screen instructions



\## 4. Install .NET SDKs (Script Method)

Once Linux Mint is installed, open a terminal. Run the following to install .NET without package manager conflicts.



\### Download and Execute Install Script

```bash

\# Get the official script

curl -sSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh

chmod +x dotnet-install.sh



This is the Microsoft supported method to install .Net. You can also use the package manager. This script will be saved in your "home" folder, so you'll now have a file called "dotnet-install.sh".



\# Install .NET 10 (For Linux/Wayland)

./dotnet-install.sh --channel 10.0



\# Install .NET 6 (For Windows Build)

./dotnet-install.sh --channel 6.0

```



The files for .Net are not installed in a hidden ".dotnet" folder in your home directory.



"dotnet-install.sh" can now be deleted.



\### Configure Environment Variables

Add the .NET paths to your shell configuration:

```bash

echo 'export DOTNET\_ROOT=$HOME/.dotnet' >> \~/.bashrc

echo 'export PATH=$PATH:$HOME/.dotnet:$HOME/.dotnet/tools' >> \~/.bashrc

source \~/.bashrc

```



Alternatively open the file using "nano \~/.bashrc"



\## 5. Verification

Ensure both SDKs are visible to the system:

```bash

dotnet --list-sdks

```

> You should see both `6.0.x` and `10.0.x` in the output list. If you get a "command not fount", reopen the terminal window.



\## 6. Clone the Project

Create a dedicated workspace and pull the source code:

```bash

mkdir -p \~/src

cd \~/src

git clone https://github.com/notBald/Avalonia86.git

cd Avalonia86

```



You now have a folder in your home folder called "src". You can call this folder whatever you like.



\## 7. Execute the Build

Prepare the script and run the compilation process:

```bash

\# Ensure the script has execute permissions

chmod +x build.sh



\# Start the build

./build.sh

```



\## 8. Output Location

After the build completes, the output files are located in the following directories:



\*   \*\*Linux (Wayland):\*\* `\~/src/Avalonia86/Avalonia86/bin/Release/net10.0/publish/`

\*   \*\*Windows:\*\* `\~/src/Avalonia86/Avalonia86/bin/Release/net6.0/publish/`



But the script will put the final files here:

\*   `\~/src/Avalonia86/pub/



