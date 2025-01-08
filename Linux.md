# How to run Avalonia 86 on Linux

![Desktop](/images/Linux.png?raw=true)

## Downloads

- [Microsoft .NET 9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- [86Box AppImage](https://github.com/86Box/86Box/releases/)
- [86Box roms](https://github.com/86Box/roms/releases)

## Installation

# Step 1.1 - The .NET runtime

Download the (x64) .NET 9 runtime and unpack its files into a folder called `dotnet`. Put this folder into your `home` directory.

# Step 1.2 - Edit .bashrc

1.  Opened a terminal in your home directory (right click -> open terminal, on most distros).
2.  Start the Nano text editor with the command: `nano ~/.bashrc`
3.  At the bottom of the file, add the following:
    ```bash
    export DOTNET_ROOT=$HOME/dotnet
    export PATH=$PATH:$HOME/dotnet
    ```
4.  Save by pressing `Ctrl+O`, then press Enter to confirm.
5.  Exit Nano with `Ctrl+X`.
6.  Close the terminal.

# Step 1.3 - Test dotnet

Reopen the terminal (so that it gets the settings above applied)

Type `dotnet --info`. You should get output similar to this:

```bash
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

Open a terminal in the folder where you put Avalonia 86. Start it with: `./Avalonia86`

If it does not start up, try typing the command `chmod +x Avalonia86` and try again.

# Step 2.2 - Creating a desktop shortcut

Open a text editor and copy in this:

```ini
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

Then replace `<user name>` and adjust the paths to point at the location where you put Avalonia 86.

Save the file on the desktop with the name _Avalonia 86.desktop_

# Step 2.3 - Run Avalonia 86

Hopefully you now have Avalonia runnning

# Step 3.1 - Setup 86Box for Linux

Download an x64 Linux Appimage from [here](https://github.com/86Box/86Box/releases/). Create a new folder in your home directory and put the AppImage into it.

There are many different distributions of linux. On Linux Mint I didn't have to do anything special, but on
Ubuntu I had to run a few commands:

So, you may have to:

- The image needs to be executable. Open a terminal in the folder and write: `chmod +x <name of AppImage>`.
- AppImages needs FUSE to run, so write: `sudo apt install libfuse2` or `sudo apt-get install fuse libfuse2`.

Now test the image by running it: `./<name of AppImage>`

If it works, and you'll get this message:

![Missing roms](/images/86Box_error.png)

Download those roms from [here](https://github.com/86Box/roms/releases).

Extract the "rom" files into a folder called roms, that you place alongside the AppImage.

# Step 3.2 - Setup Avalonia 86

Avalonia 86 needs a VM folder and a 86Box executable appimage. This has to be set up in the Program Settings:

![Tools menu](/images/Linux_2.png?raw=true)

The VM folder can be whatever folder you please. It only serves as the default folder when you don't specify a folder in the Add VM dialog.

The 86Box folder has to point to a folder that contains an executable file that starts with the name `86Box`, exactly as typed (with the same capitalization).

Point Avalonia 86 to said folder.

# Step 3.3 - Create a Virtual Machine

Select `New Virtual Machine` from the file menu

![File menu](/images/Linux_3.png?raw=true)

# Step 3.4 - Type a name

Name is the only field you have to type; all others are optional.

![File menu](/images/Linux_4.png?raw=true)

You should hopefully now be able to run the machine. Good luck.
