Script Center Example for MibExplorer

Contents:
- /SingleScript.sh          -> simple script example
- /PackageScript/run.sh      -> package entry point example
- /PackageScript/scripts/... -> secondary helper scripts
- /PackageScript/data/...    -> data files used by the package

How Script Center detects scripts:
1. Any .sh file placed directly inside Scripts/ is shown as a simple script.
2. Any folder placed directly inside Scripts/ that contains run.sh is shown as a package.
3. All other files/folders inside a package are treated as dependencies and are not shown separately.

Package execution model:
- The whole package folder is uploaded to /tmp/<generated_package_name>/ on the MIB.
- Script Center runs: cd /tmp/<generated_package_name> && sh ./run.sh
- So run.sh can freely use relative paths to helpers and data files.
