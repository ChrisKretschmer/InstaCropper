-- InstaCropper macOS droplet.
--
-- Compiled by macos/build-macos-app.sh into InstaCropper.app. Dragging image
-- files onto the app icon triggers the `on open` handler below, which asks for
-- the aspect ratio and background color, then invokes the bundled .NET binary.
--
-- Every file path is passed through AppleScript's `quoted form`, which is the
-- fix for the long-standing bug where paths containing spaces were split into
-- multiple arguments and mangled.

on open theFiles
	-- Aspect ratio (labels must match InstaCropper's menu names).
	set ratioOptions to {"1:1 (1080x1080)", "4:5 (1080x1350)", "16:9 (1080x608)"}
	set chosenRatio to (choose from list ratioOptions ¬
		with prompt "Select the Instagram aspect ratio:" ¬
		default items {"4:5 (1080x1350)"})
	if chosenRatio is false then return
	set ratioArg to item 1 of chosenRatio

	-- Background color.
	set colorOptions to {"White", "Black"}
	set chosenColor to (choose from list colorOptions ¬
		with prompt "Select the background color:" ¬
		default items {"White"})
	if chosenColor is false then return
	set colorArg to item 1 of chosenColor

	-- Pick the binary matching the current CPU architecture.
	set appPath to POSIX path of (path to me)
	set arch to do shell script "uname -m"
	if arch is "arm64" then
		set binPath to appPath & "Contents/Resources/bin/osx-arm64/InstaCropper"
	else
		set binPath to appPath & "Contents/Resources/bin/osx-x64/InstaCropper"
	end if

	-- Build a properly quoted argument string for every dropped file.
	set fileArgs to ""
	repeat with f in theFiles
		set fileArgs to fileArgs & " " & quoted form of (POSIX path of f)
	end repeat

	set theCommand to quoted form of binPath ¬
		& " --ratio " & quoted form of ratioArg ¬
		& " --color " & quoted form of colorArg ¬
		& fileArgs

	try
		do shell script theCommand
		try
			display notification ("Cropped " & (count of theFiles) & " image(s).") with title "InstaCropper"
		end try
	on error errMsg
		display dialog "InstaCropper failed:" & return & errMsg buttons {"OK"} default button "OK" with icon stop
	end try
end open

-- Double-clicking the app (no files) just explains how to use it.
on run
	display dialog "Drag one or more image files onto the InstaCropper icon to fit them onto an Instagram canvas." buttons {"OK"} default button "OK"
end run
