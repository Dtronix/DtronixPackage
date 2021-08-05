# ![Logo](https://github.com/Dtronix/DtronixPackage/blob/master/src/icon.png) DtronixPackage ![.NET Core Desktop](https://github.com/Dtronix/DtronixPackage/actions/workflows/dotnet.yml/badge.svg) [![NuGet](https://img.shields.io/nuget/v/DtronixPackage.svg?maxAge=600)](https://www.nuget.org/packages/DtronixPackage)

Dtronix package is a save file management system for storing & retrieving save data for applications.

#### Features
- Simple structure utilizing zip files for content management.
- Data compression via the Deflate compression method.
- Lock file management to enable networked usage notification of package files via lock files.
- Integrated versioning & upgrade system.
- Read only opening ability.
- Save content management and modification notification.
- Integrated writing & reading of JSON, strings & stream data.
- Auto-Save methods to allow for background saving for crash recovery.
- Backup save system keeps a duplicate of save packages for simple recovery.
- No external dependencies.
- Uses new performant System.Text.Json classes.
- Package management view model for easy integration into WPF applications.

#### Building
```
dotnet publish -c Release
```

#### License

Program is licensed under the MIT license.
Logo is licensed under the [Creative Commons Attribution 3.0 License](http://creativecommons.org/licenses/by/3.0/us/) by [FatCow](https://www.fatcow.com/free-icons)