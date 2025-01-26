# Building a AppImage for Linux

This is a little tricky, so I decided to type down notes before I forget.

## Publish

The easy part is to Publish:
 1. Right click on the Avaloina 86 project and click "publish"
 2. Create a new folder profile
 3. Select a self-contained Linux-x64 or arm deployment
 4. Output the files to a directory Linux can access, assuming you're using a VM

## Setting up Linux

### .net

You need to set up dotnet 8.0. I'm not sure how to do this, as the install failed for me.
I instead messed around until I got things working, but assuming a clean install of
Linux Mint I think all you need to do is run the install script found here:

https://learn.microsoft.com/en-us/dotnet/core/install/linux-scripted-manual#scripted-install

### PupNet

This is the tool we'll use to make the AppImage. To set it up, assuming .net works, you
run the command: ```dotnet tool install -g KuiperZone.PupNet```

There is also a AppImage version of PupNet. That one does not need .net installed first,
so might be the easier option.

### Folders and files

You need a folder for a config file, and a subfolder for some additional files.
 - In the root, put the ```Avalonia86.pupnet.conf``` file.
 - Create a subfolder called "Deploy"
 - Put ```PostPublish.bat``` and ```PostPublish.sh``` in the Deploy folder.
 - Also put a svg icon called ```Avalonia86.svg``` in the Deploy folder.

 #### PostPublish.bat

 This file is not used, but PupNet will complain if it isn't there

 #### PostPublish.sh

 This file copies the Avalonia86 files to the correct folder. If you put them in a
 subfolder called "bin", then you don't have to modify this script.

 #### Avalonia86.pupnet.conf

 There is a version number you need to manually modify in this file, but besides that
 it sould be fine as it is.

 ### Building

 Run the command: ```pupnet --kind appimage``` or ```pupnet --runtime linux-arm64 --kind appimage```

 There is also an option for supplying a ```--app-version 4.0.0``` command, which can
 be used innstead of manually editing the conf file.