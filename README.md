# SVG2PNG-AzureFunction
Example function that calls Batik JAR command line file and returns an image object

The purpose of this function app is to convert SVG files to PNG's, specifically SVG's with embedded fonts.  There are great conversion tools out there like:
* https://svgtopng.com/ for one time conversions
* https://cloudconvert.com/svg-to-png for general purpose conversion via API call, but does not support embedded fonts
* https://www.imagemagick.org/script/index.php and https://archive.codeplex.com/?p=svg are also good .NET libraries, but again, neither supports embedded fonts.

Functionally, this app is a good tutorial for doing the following:
* Calling Java.exe command line from C# to run a JAR file
* Using Environment Variables in an Azure Function
* Returning an image in an Azure Function
* Using temp storage to download and manipulate files in an Azure Function
* Pushing files to Azure Blob Storage from an Azure Function

## Usage  
[FunctionURL]?l=[URL of SVG File]

## Sample Files
You can find a bunch of SVG Samples here: https://dev.w3.org/SVG/tools/svgweb/samples/svg-files/

## Notes
I'm pretty new at building for Azure and this was my first project.  Happy to update based on suggested best practice.  Also happy to share my learning experience.
