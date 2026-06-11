# Purpose

Create an application that will select a video or music file based on user input.

# Configuration

Application will take a configuration and have only one as a json file in the working directory of the application.  

## Application Settings:

	- Directories To Search
		- video
		- audio

# User input
	- User will put in tags to search for N random media that match.
	- N - Number of media to return	
	- A button to try the search again.
	
# Output
	
	- Random list of matches for N matches.
	- Each list item will have a link to load those the item file with the default associated application. A simple "Link" text as a clickable item to load the file.
	- Each list item will also have a description of the item using 70 percent of the width of the application.  The description should be built from file title and tjhen file metadata until 400 characters are achieved.
	
	
# UI
	
## Style

	- Minimalist
	- Resiable
	- Minimum borders unless they can be hidden completely.
	- No title.
	- [Agent Instruction] Suggest a color palette of muted colors.
	
## Components

	- One button to refresh/reroll/search picks.
	- Feel Lucky button
	- Text box for tags to be entered
	- List box with items listed.
		- Short description of item (50% width of window)
		- "Link" with file link to open in default application.
	
	
# Implementation

UI and business logic will be separate.  A console app and graphic UI will tie into a simple class library.  Each UI will be a separate project using the class library by project reference.

The application will search recursively in a given directory for either audio or video.

For audio, the link will link to the directory playlist of the audio match.  Create the playlist if it is not already existing.
For video, the link will link to the largest video file in the case of movies or a random episode file in the case of television.  Movies will be distinguished from television based on context clues.

Context clues.
- File size
- File metadata
- Parent Directories
- File title
- IMDB Search
- Google

## Heuristics
	- Stripped Path - see Stripped File and Directory Names
	- 


## Stripped File and Directory Names
	- Remove bracketed ([]) text.
	- Remove extension.