# YAFNET.LanguageManager

![build status](https://github.com/yafnet/yaf.languagemanager/actions/workflows/build.yml/badge.svg)
[![NuGet](https://img.shields.io/nuget/v/YAFNET.LanguageManager.svg)](https://nuget.org/packages/YAFNET.LanguageManager)

A .NET global tool for synchronizing, minifying, and automatically translating [YetAnotherForum.NET](https://www.yetanotherforum.net/) JSON language files.

It keeps every `*.json` language file in a folder in sync with `english.json` — adding missing pages/resources, removing resources that no longer exist in the source, and optionally auto-translating new or untouched strings via Google Translate.

## Prerequisites

* .NET 10 SDK or later (runtime is enough to run the tool once installed)

## Installation

Install as a global .NET tool:

```console
dotnet tool install -g YAFNET.LanguageManager
```

Update to the latest version:

```console
dotnet tool update -g YAFNET.LanguageManager
```

Uninstall:

```console
dotnet tool uninstall -g YAFNET.LanguageManager
```

The tool is installed as `yaf-langmgr`.

## Usage

```console
yaf-langmgr <pathToLanguageFiles> [options]
```

The target folder must contain `english.json` (the source of truth) alongside the other `<language>.json` files to keep in sync.

### Arguments

| Argument             | Description                                                                    |
|----------------------|----------------------------------------------------------------------------------|
| `pathToLanguageFiles` | Path to the folder containing `english.json` and the other language files. Relative paths are resolved against the current directory. |

### Options

| Option              | Description                                                                 |
|---------------------|-------------------------------------------------------------------------------|
| `-sync`              | Synchronize all language files against `english.json`: adds missing pages/resources (auto-translating new strings) and removes resources that no longer exist in the source. |
| `-translateGoogle`   | Auto-translate resources whose text still matches the English source, using the free Google Translate endpoint. |
| `-minify`            | Minify all language files (compact JSON, no indentation).                    |
| `-uglify`            | Un-minify all language files (pretty-printed, indented JSON).                |
| `-help`, `-?`        | Show usage help.                                                              |

## Examples

Synchronize all language files in a folder with `english.json`, translating any new strings:

```console
yaf-langmgr C:\YAF\Languages -sync
```

Synchronize and machine-translate any strings still left in English:

```console
yaf-langmgr C:\YAF\Languages -sync -translateGoogle
```

Minify all language files before shipping a release:

```console
yaf-langmgr C:\YAF\Languages -minify
```

Pretty-print previously minified language files for editing:

```console
yaf-langmgr C:\YAF\Languages -uglify
```

## License

*YAFNET.LanguageManager* is licensed under the Apache 2.0 license.

### Yet Another Forum Community Support

If you have any questions, please visit the YAF Community Support forum: [https://forum.yetanotherforum.net](https://forum.yetanotherforum.net), or visit the Wiki for More Informations.
