# Cut Chart Command-Line Interface (cc-cli.exe)

Do you want to work with Hypertherm Cut Chart data from your command line?

Simply download our Cut Chart CLI executable file and use the commands below.

This command-line interface uses the Hypertherm Cut Chart API and its [OpenAPI specification](https://cutcharts.azurewebsites.net/api/docs).

## Installation

Download and save the executable (.exe) file from the [cutchart-cli releases](https://github.com/hypertherm/cutchart-cli/releases) page into the local folder of your choice.

## Get started

From your command line, navigate to the folder where the executable file is saved.

Next, type the name of the executable followed by a:

 1. A [basic command](#basic-commands) (`families`, `models`, `customs`, `cutchart`, or `versions`(Coming soon))
 2. An [optional argument](#optional-arguments) (if desired)

## Basic commands

The following commands are available for working with cut chart data:

- **`families`**: Lists tool families with available cut chart data, such as Powermax or MAXPRO.
- **`models`**: Lists the tool models in a given family with available cut chart data, such as 105 or 200.
- **`cutchart`**: Downloads cut chart data for a given tool family and model.
- **`customs`**: Downloads modified cut chart data based on the provided XML schema.
- **`versions`(Coming soon)**: Lists the versions for a given family and model.

### Basic use example

Find out what Hypertherm product families have cut chart data available.

	cc-cli families
	
The request and response for the families endpoint could look like this:
![This is an example](https://github.com/hypertherm/cc-cli/blob/ART-5857-Update-README-for-cc-cli.exe/imgs/BasicExample_Which_Families_Have_Cut_Charts.jpg?raw=true)

## Optional arguments

These are the available command arguments:

 - **[-h | --help]**
 - **[-f | --family]**
 - **[-m | --model]**
 - **[-o | --outfile]**
 - **[-x | --xmlfile]**
 - **[-u | --units]**
 - **[-t | --type]**
 - **[-l | --logout]**
 - **[-v | --version]**

### Examples

- Find out what commands and arguments are available.

      cc-cli -h

     The request and response for viewing commands and arguments could look like this:
	![This is a graphic example of requesting help from the command line](https://github.com/hypertherm/cc-cli/blob/ART-5857-Update-README-for-cc-cli.exe/imgs/Example_SeeHelp.jpg?raw=true)
 
- Find the Powermax models with available cut chart data.

      cc-cli models -f powermax
      
     The request and response for identifying supported Powermax models could look like this:
	![This is a graphic example of identifying supported Powermax models from the command line](https://github.com/hypertherm/cc-cli/blob/ART-5857-Update-README-for-cc-cli.exe/imgs/Example_Models_with_CCdata.jpg?raw=true)

- Download modified Powermax105 cut chart data, based on the provided XML file.

      cc-cli customs -f powermax -m 105 -x myStyle.xml -o cc.xlsx
      
     The request and response for uploading an XML file (*myStyle.xml*) to the customs endpoint and downloading the updated Powermax105 cut chart data outfile (*cc.xlsx*) could look like this:
     ![This is an example of customizing cut chart data with an XML file from the command line](https://github.com/hypertherm/cc-cli/blob/ART-5857-Update-README-for-cc-cli.exe/imgs/Example_custom-cc-request.jpg?raw=true)
     
     **In this example:**
     
     - **myStyle.xml** defines how to customize cut chart data. This file must be saved in the same location as the cc-cli executable file before making the request. For testing purposes, you can use the *myStyle.xml* file provided in this repo. 
	
     - **cc.xlsx** is automatically created to provide updated cut chart data. Once the "Success" response is returned, look for this file in the same location as your XML file.
     
     Name the XML file and the outfile whatever you want. 
