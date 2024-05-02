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
- Mode: Select the generation mode for the resulting spritesheet, this will affect the resulting quality and fluidity of the spritesheet, see the [Generation mode tips](#generation-mode-tips) section.
- Crop: Allows you to select a specific area of the GIF to be converted.
- ChromaKey: Allows you to apply a chroma key filter to the GIF, click on the colored button then on the GIF to select a color to filter.

## Generation mode tips

VRChat limits the size and number of frames on the spritesheet, depending on the amount of frames, the quality of each frame will change:
- Quality: 4 frames at 512x512 pixels.
- Balance: 16 frames at 256x256 pixels.
- Fluidity: 64 frames at 128x128 pixels.

If you don't know what you are doing, in most cases Fluidity should be the best option.
