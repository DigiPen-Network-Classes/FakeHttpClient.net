# Fake Http Client (.NET Version)

This is some test-automation to be used with CS 260's Assignment 3:
the Http Proxy assignment. 

Originally all of this was written using .BAT but increasingly, getting that 
to work is becoming an issue. Then we tried PowerShell, and that was worse!

This attempt is built on .NET 8.0 and packages into a self-contained EXE,
which should run on any Windows machine without needing to install .NET.

# Installation Instructions

Go to the [Releases](https://github.com/DigiPen-Network-Classes/FakeHttpClient.Net/releases)
and find the latest release that matches your architecture and platform.
Download the ZIP and extract it to a directory of your choice.

You can test that it's working by opening a PowerShell terminal and running:
```pwsh
./FakeHttpClient.Net.exe --help
```

If you see something other than the help screen, then you should follow up with 
the instructor and/or TAs.

## Usage

First, start your CS260_Assignment3 program, either in the Visual Studio Debugger or
from the command line. Make sure that your program is listening on port 8888.
(If for some reason you need to change the port, you can do so by passing the `-Port` argument, 
see the help screen for details.)

Once that's running, you can execute the FakeHttpClient to test your proxy server.
Some .BAT files have been provided to make testing a little easier:

### RunOne.bat

Requests a single URL and prints the results to the console. You should see the test start,
HTML scroll by the console, ending about 5 seconds later.

### RunOneGoogle.bat

Requests a single URL (www.google.com) and prints the results. This will let you test URLs
that return larger amounts of data -- are you handling multiple chunks of data correctly?

### RunOneFileOutput.bat

This calls the http://cs260.meancat.com/delay URL, outputting the results to a file.


### RunManyFileOutput.bat

This is the big test -- running this configuration will execute many HTTP requests concurrently.
If the test is successful, you should see a number of files created in the same directory as 
FakeHttpClient.exe, and you should note the timing of each request -- they should be approximately
the same amount of time (give or take a second) -- if the first takes 5 seconds, the 2nd takes
10 seconds, the 3rd takes 15 seconds, etc., then you have a problem!

## File Results

On success, there should be a number of "(test-name)-(number).txt" files, depending on how many
URLs you were requesting concurrently.

# Example Output

You should see something like this, although your specific output *will be different*:

```pwsh
    ./FakeHttpClient.exe --url http://cs260.meancat.com/delay --interactive true
```

```html
Begin - 6/19/2025 5:55:05PM
Begin Test : http://cs260.meancat.com/delay 6/19/2025 5:55:05PM
HTTP/1.1 200 OK
Server: nginx/1.18.0 (Ubuntu)
Date: Fri, 20 Jun 2025 00:55:05 GMT
Content-Type: text/html; charset=utf-8
Transfer-Encoding: chunked
Connection: close
X-Powered-By: Express
Cache-Control: no-cache

48c
<!-- Padding to encourage flush -->                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                <br><html><head><title>DigiPen CS 260 Delayed Response Test</title></head><body><h2>Here we go!</h2>1<br>
5
2<br>
5
3<br>
5
4<br>
5
5<br>
5
6<br>
5
7<br>
5
8<br>
5
9<br>
6
10<br>
6
11<br>
6
12<br>
6
13<br>
6
14<br>
6
15<br>
6
16<br>
6
17<br>
6
18<br>
6
19<br>
6
20<br>
6
21<br>
6
22<br>
6
23<br>
6
24<br>
6
25<br>
6
26<br>
6
27<br>
6
28<br>
6
29<br>
6
30<br>
6
31<br>
6
32<br>
6
33<br>
6
34<br>
6
35<br>
6
36<br>
6
37<br>
6
38<br>
6
39<br>
6
40<br>
6
41<br>
6
42<br>
6
43<br>
6
44<br>
6
45<br>
6
46<br>
6
47<br>
6
48<br>
6
49<br>
26
50<br><h2>All done!</h2></body></html>
0


End Test: Total time for  (http://cs260.meancat.com/delay): 5116ms. Result: pass!
End - 6/19/2025 5:55:10 (elapsed: 5122 ms)
```
