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
(If for some reason you need to change the port, you can do so by passing the `--proxy-port` argument, 
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

## Request Modes

Real clients don't all send requests the same way, and your proxy has to handle each of them.
By default the FakeHttpClient sends the whole request at once and then shuts down its sending side.
These modes change that:

* `NoHalfClose` keeps the sending side open while waiting for the response, like curl or a browser.
  If your proxy waits for `recv()` to return 0 before it reads the request, this test will hang and
  report `FAIL! (Timeout)`. Read until you see the blank line (`\r\n\r\n`) instead.
* `SplitRequest` sends the request in several pieces with short pauses between them, including splitting
  the final `\r\n\r\n`. If your proxy reads the request with a single `recv()`, it will only see part of it.
* `LargeRequest` adds extra headers so the request is about 10 KB. If your proxy uses a small fixed-size
  buffer, or reads the request with a single `recv()`, it will only see part of it.

`multi.json` includes one test for each mode. To try a mode on a single URL:

```pwsh
./FakeHttpClient.exe --url http://cs260.meancat.com/delay --mode NoHalfClose
./FakeHttpClient.exe --url http://cs260.meancat.com/delay --mode "NoHalfClose,SplitRequest,LargeRequest"
```

In a test suite file, add a `"Mode"` field to a test (for example `"Mode": "SplitRequest"`).
Modes only apply to the raw TCP client (`--raw true`, the default).

## Reading the Results

Each test ends with a summary like this:

```
End Test: Total time for delay-dns-1 (http://cs260.meancat.com/delay): 5076ms.
Bytes Received: 1931
Response: status 200, body 1482 bytes, SHA-256 1faae000a81...
 Result: pass!
```

The `Response` line comes from parsing what your proxy sent back. The body length and SHA-256
are measured after removing chunked encoding, so the same page always gives the same values;
if two runs of `/delay` give different hashes, something is changing the data. Possible results:

| Result | Meaning |
|---|---|
| `pass!` | the response arrived complete, in time |
| `TooSlow` | it took longer than allowed (usually 7 seconds): requests may be handled one at a time |
| `Timeout` | your proxy never closed the connection |
| `Incomplete` | the connection closed before the whole response arrived (short of `Content-Length`, or no final chunk) |
| `ConnectionReset` | your proxy reset the connection instead of closing it gracefully |
| `NotEnoughData` | almost nothing came back |
| `Error` | something else went wrong; see the `Error:` line above it |

Test suites can also check the status, body length, or SHA-256 (`ExpectStatus`, `ExpectBodyLength`,
`ExpectSha256`) and set a time limit (`MaxMs`); a mismatch shows as `WrongStatus`, `WrongLength`,
or `WrongContent`. Suites can also change timing: `StallMs` makes a test connect and then send
nothing for that long (its time limit grows by the same amount), and `StartDelayMs` makes a test
wait before connecting. Output files stop after the first 1 MB of each response, but the whole
response is still counted and checked.

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

# Preparing a new Release

Ex. current working version is `1.2.0-alpha+gxxxx` and we're ready to create a release branch
for version 1.2. The main branch should now be `1.3.0-alpha+gxxxxx`

```bash
nbgv prepare-release
git checkout release/v1.2.0
nbgv get-version

pwsh ./build-all.ps1
pwsh ./deploy-release.ps1
git push --set-upstream origin release/v1.2.0
```
