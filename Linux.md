# How to run Avalonia 86 on Linux

![Desktop](/images/Linux.png?raw=true)

## Downloads
 - [Microsoft .NET 9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)

## Installation

# Step 1.1 - The .Net runtime

Download the (x64) .Net 9 runtime and unpack its files into a folder called "dotnet". Put this folder into your home directory.

# Step 1.2 - Edit .bashrc

 1. Opened a terminal in your home directory
 2. Start the Nano text editor with the command: nano ~/.bashrc
 3. At the bottom of the file, add the following:<br>
    export DOTNET_ROOT=$HOME/dotnet<br>
    export PATH=$PATH:$HOME/dotnet
 4. Save by pressing (right ctrl)+O, then pressed enter.
 5. Exit Nano with (right ctrl)+X
 6. Close the terminal

# Step 1.3 - Test dotnet

Reopen the terminal (so that it gets the settings above applied)

Type "dotnet --info". You should get output similar to this:
```
VirtualBox:~$ dotnet --info

Host:
  Version:      9.0.0
  Architecture: x64
  Commit:       9d5a6a9aa4
  RID:          linux-x64

.NET SDKs installed:
  No SDKs were found.
```

# Step 2.1 - Testing Avalonia 86

Open a terminal in the folder where you put Avalonia 86. Start it with: *./Avalonia86*

If it does not start up, try typing the command *chmod +x Avalonia86* and try again.

# Step 2.2 - Creating a desktop shortcut

Open a text editor and copy in this:
```
[Desktop Entry]
Version=1.0
Type=Application
Name=Avalonia86
Exec=bash -c "export DOTNET_ROOT=/home/<user name>/dotnet && /home/<user name>/Avalonia86-folder/Avalonia86"
Path=/home/<user name>/Avalonia86-folder/
Icon=/home/<user name>/Avalonia86-folder/Resources/86Box-gray.svg
Comment=Manager
Terminal=false
Categories=Game;Emulator;
StartupWMClass=86box-vm
StartupNotify=true
Name[en_US]=Avalonia 1.0
```

Then replace &lt;user name&gt; and adjust the paths to point at the location where you put Avalonia 86.

# Step 2.3 - Run Avalonia 86

Hopefully you now have Avalonia runnning

# Step 3.1 - Setup Avalonia 86

Avalonia 86 needs a VM folder and a 86Box executable appimage. This has to be set up in the Program Settings:

![Tools menu](/images/Linux_2.png?raw=true)

The VM folder can be whatever folder you please. It's the default folder a VM is created in when you don not specify a folder for a VM.

The 86Box folder has to point to a folder that contains a file called *86Box.AppImage*, exactly as typed (with the big and small letters).

Download an Appimage from [here](https://github.com/86Box/86Box/releases/tag/v4.2.1). Then rename it to *86Box.AppImage*

Point Avalonia 86 to said folder

# Step 3.2 - Create a Virtual Machine

Select *New Virtual Machine* from the file menu

![File menu](/images/Linux_3.png?raw=true)

# Step 3.3 - Type a name

Name is the only field you have to type, all others are optional.

![File menu](/images/Linux_4.png?raw=true)

You should hopefully now be able to run the machine. Good luck.