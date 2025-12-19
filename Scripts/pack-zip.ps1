Remove-Item ..\QuickLook.Plugin.PDFViewer-Native.qlplugin -ErrorAction SilentlyContinue

$files = Get-ChildItem -Path ..\Build\Release\ -Exclude *.pdb,*.xml
Compress-Archive $files ..\QuickLook.Plugin.PDFViewer-Native.zip
Move-Item ..\QuickLook.Plugin.PDFViewer-Native.zip ..\QuickLook.Plugin.PDFViewer-Native.qlplugin