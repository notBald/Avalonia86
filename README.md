# 86Box Manager
**86Box Manager** is an optional configuration manager for the [86Box emulator](https://github.com/86Box/86Box). It's released under the MIT license, so it can be freely distributed with 86Box. See the `LICENSE` file for license information and `AUTHORS` for a complete list of contributors and authors.

It's written in C# with Avalonia.

![Desktop](/images/UI.png?raw=true)

## Features
* Create / Delete Virtual Machines
* Sort them into categories
* Displays machine information and images
* Support comments and descriptions
* A tray icon so the Manager window doesn't get in your way

## System requirements
System requirements are the same as for 86Box. Additionally, the following is required:  

* [86Box 3.0](https://github.com/86Box/86Box/releases) or later (earlier builds don't support all the features the Manager expects)
* [.NET 9.0](https://dotnet.microsoft.com/download/dotnet/9.0)

## How to use
1. Download the desired build [here](https://github.com/notBald/Avalonia86/releases)
2. Run `Avalonia86.exe`
3. Go to Settings, choose the folder where `86Box.exe` is located (along with the roms folder) and a folder where your virtual machines will be located (for configs, nvr folders, etc.)
4. Start creating new virtual machines and enjoy

## How to build
1. Clone the repo
2. Open `Avalonia86.sln` solution file in Visual Studio
3. Make your changes
4. Choose the `Release` or `Debug` configuration
5. Build the solution
