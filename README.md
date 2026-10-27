# IntuneWin Packager

A small Windows Forms front end for Microsoft's **IntuneWinAppUtil.exe**
([Win32 Content Prep Tool](https://github.com/microsoft/Microsoft-Win32-Content-Prep-Tool)).
Pick an input folder and an output folder, tick the installers you want, and it builds a
`.intunewin` package for each one so you can upload it as a Win32 app in Intune.

## Usage

1. Open `IntuneWinPackager.sln` in Visual Studio and press F5 (or build it; needs .NET Framework 4.7.2).
2. **IntuneWinAppUtil.exe** – click *Browse...* and pick the tool. It's found automatically if it sits next to
   `IntuneWinPackager.exe`, in the input folder, or on your `PATH`. *Download...* opens Microsoft's GitHub page.
3. **Input folder** – the folder that holds your apps. It is scanned for `.msi`, `.exe`, `.ps1`, `.cmd`
   and `.bat` files (tick *Include subfolders* to search below it).
4. **Output folder** – where the `.intunewin` files go.
5. Tick the setup files you want and click **Compile selected**. Progress and the tool's output appear in the log.

Your folders are remembered between runs in `%APPDATA%\IntuneWinPackager\settings.txt`.

## How each app is packaged

For every ticked setup file the program runs:

```
IntuneWinAppUtil.exe -c "<folder the setup file is in>" -s "<setup file>" -o "<output>\<same subfolder>" -q
```

* `-c` is the setup file's own folder, so **everything in that folder (and its subfolders) goes into the package**.
* The output keeps the input folder's layout, so two apps that both have a `setup.exe` don't overwrite each other.
* `-q` runs quietly and overwrites an existing `.intunewin` with the same name.

A tidy input folder works best, one folder per app:

```
Input\
  7-Zip\7z2408-x64.msi              ->  Output\7-Zip\7z2408-x64.intunewin
  Chrome\GoogleChromeEnterprise.msi ->  Output\Chrome\GoogleChromeEnterprise.intunewin
  MyApp\Install.ps1  (+ files)      ->  Output\MyApp\Install.intunewin
```

Avoid putting setup files directly in the input folder root: packaging one of those includes the whole input folder.
