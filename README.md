# ![Logo](https://github.com/Dtronix/DtronixPackage/blob/master/src/icon.png) DtronixPackage ![.NET Core Desktop](https://github.com/Dtronix/DtronixPackage/workflows/.NET%20Core%20Desktop/badge.svg)

Dtronix package is a save file management system for storing & retrieving save data for applications.

#### License
Program is licensed under the MIT license.
Logo is licensed under the [Creative Commons Attribution 3.0 License](http://creativecommons.org/licenses/by/3.0/us/) by [FatCow](https://www.fatcow.com/free-icons)

#### Features
- Simple structure utilizing zip files for content management.
- Data compression via the Deflate compression method.
- Lock file management to enable networked usage of save files and notification locked files.
- Versioning & upgrade system.
- Read only opening.
- Save content management and modification notification.
  - Writing & Reading of JSON, String, Stream data.
- Auto-Save methods to allow for background saving for crash recovery.
- Backup save system.
- Uses new performant System.Text.Json classes.
- Single NLog dependency
- File management view model for easy integration into WPF applications.

#### Building
```
dotnet publish -c Release
```

