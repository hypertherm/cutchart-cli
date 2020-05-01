# Cut Chart Command-Line Interface (cc-cli.exe)

Do you want to work with Hypertherm Cut Chart data from your command line?

Simply download our Cut Chart CLI executable file and use the commands below.

This command-line interface uses the Hypertherm Cut Chart API and its [OpenAPI specification](https://cutcharts.azurewebsites.net/api/docs).

## Installation

### Minimum requirements

- Windows 7, 8, or 10

Download and save the executable (.exe) file from the [cutchart-cli releases](https://github.com/hypertherm/cutchart-cli/releases) page into the local folder of your choice.

## Get started

From your command line, navigate to the folder where the executable file is saved.

Next, type the name of the executable followed by a:

 1. A [basic command](#basic-commands) (`products`, `cutchart`, `customs`)
 2. An [optional argument](#optional-arguments) (if desired)

## Basic commands

The following commands are available for working with cut chart data:

- **`products`**: Lists products with available cut chart data, such as Powermax105 or MAXPRO200.
- **`cutchart`**: Downloads cut chart data for a given product.
- **`customs`**: Downloads modified cut chart data based on the provided XML schema.

### Basic use example

Find out what Hypertherm products have cut chart data available.

	cc-cli products

The request and response for the products endpoint could look like this:

![This is an example](https://github.com/hypertherm/cc-cli/blob/master/images/Basic-Example-Get-Products.jpg?raw=true)

## Optional arguments

These are the available command arguments:

- **[-h | --help]**
- **[-v | --version]**
- **[-u | --update]**
- **[-d | --dumplog]**
- **[-c | --clearlog]**
- **[-p | --product]**
- **[-o | --outfile]**
- **[-x | --xmlfile]**
- **[-u | --units]**
- **[-t | --type]**
- **[-l | --logout]**

### Examples

- Find out what commands and arguments are available.

      cc-cli -h

     The request and response for viewing commands and arguments could look like this:
	![This is an example request for help from the command line](https://github.com/hypertherm/cc-cli/blob/master/images/Example_SeeHelp_v1.2.0.jpg?raw=true)

- Download modified Powermax105 cut chart data, based on the provided XML file.

      cc-cli customs -p powermax105 -x myStyle.xml -o cc.xlsx

     The request and response for uploading an XML file (*myStyle.xml*) to the customs endpoint and downloading the updated Powermax105 cut chart data outfile (*cc.xlsx*) could look like this:
     ![This is an example of customizing cut chart data with an XML file from the command line](https://github.com/hypertherm/cc-cli/blob/master/images/Example_custom-cc-request.jpg?raw=true)

     **In this example:**

     - **myStyle.xml** defines how to customize cut chart data. This file must be saved in the same location as the cc-cli executable file before making the request. For testing purposes, you can use the *myStyle.xml* file provided in this repo. 

     - **cc.xlsx** is automatically created to provide updated cut chart data. Once the "Success" response is returned, look for this file in the same location as your XML file.

     Name the XML file and the outfile whatever you want.

- Automatically check for an update to the latest version

     The Cut Chart CLI can check for new updates and automatically apply them.

      cc-cli --update

     When the Cut Chart CLI is updated, a backup of the current version is saved to "%APPDATA%\cc-cli\versions\".

- Interact with the Log file

     Print out the entire log history.
     
      cc-cli --dumplog

     Clear the log history.

      cc-cli --clearlog
