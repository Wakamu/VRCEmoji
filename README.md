# VRCEmoji
Converter tool from Gif to VRC compatible spritesheets for animated emojis

## Features

| Feature | Supported | Comments |
|:--------|:---------:|:---------|
|Timeline selection|:white_check_mark:||
|Gif cropping|:white_check_mark:||
|Generated framecount selection|:white_check_mark:||
|Automatic naming following VRC's convention|:white_check_mark:||
|Result preview|:white_check_mark:|
|Chroma keying|:white_check_mark:|HSV or RGB
|Emoji uploading|:white_check_mark:|

## How to use

- Download the latest release [here](http://github.com/Wakamu/VRCEmoji/releases/latest "Release").
- Open a GIF file using the "Open" button, the GIF will be displayed in the top Canvas.
- Set your settings in the [Settings](#settings) section.
- Press the "Generate" button to process the GIF into a spritesheet, the result preview will be displayed on the bottom Canvas.
- Press the "Save" button to save the spritesheet file into your PC, or press the "Upload" button to upload the emoji into your VRChat account.

## Settings

- Start: Select at which frame of the GIF the emoji should start (default: first frame).
- End: Select at which frame of the GIF the emoji should end (default: last frame).
- Frames: Select the total number of frames that should be in the resulting spritesheet, this will affect the resulting quality of the spritesheet, see the [Quality tips](#quality-tips) section.
- Crop: Allows you to select a specific area of the GIF to be converted.
- ChromaKey: Allows you to apply a chroma key filter to the GIF, click on the colored button then on the GIF to select a color to filter.

## Quality tips

VRChat limits the size and number of frames on the spritesheet, depending on the amount of frames, the quality of each frame will change:
- <= 4 frames : 512x512.
- <= 16 frames : 256x256.
- <= 64 frames : 128x128.
