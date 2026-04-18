#!/bin/sh
# Simple Script Center example
# You can put until 3 Lines of commented text
# Those 3 lines will be displayed in the Script Center as a description of the script

echo "=== Simple example start ==="
echo "Working directory:"
pwd

echo "Creating a temporary file in /tmp..."
echo "hello from example.sh" > /tmp/mibexplorer_example.txt

echo "Reading the temporary file..."
cat /tmp/mibexplorer_example.txt

echo "Waiting a moment so the user can observe the execution log / throbber..."
sleep 2

echo "Cleaning up..."
rm -f /tmp/mibexplorer_example.txt

echo "=== Simple example end ==="
exit 0
