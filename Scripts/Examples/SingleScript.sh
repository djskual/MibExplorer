#!/bin/sh

# Type: ReadOnly
# Example Single Script Center
# Just say hello
# you can use until 3 lines of description

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
