$path = "$PWD\Publish\DailyDash.exe"
$s = (New-Object -COM WScript.Shell).CreateShortcut("$env:USERPROFILE\Desktop\Daily Dash.lnk")
$s.TargetPath = $path
$s.Description = "Daily Dash"
$s.Save()
