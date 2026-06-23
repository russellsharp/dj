# General
1. Use UTC and ISO 8601 format for date time and timestamps.
2. Use comments when the code does not describe why it is being called.  The 'why' should be abvious from the method calls and the objects used.
3. Before implementing any production code, implement tests for Test Driven Development.  The tests may fail until the agent is ordered to implement infrastructure code.
4. For methods and functions, check input parameters for validity before they are used and throw appropriate exceptions.  Examples of invalid values are null, whitespace, and empty.
5. When prompted to do more than 30 lines of code, investigation of a concept, or the prompt includes workload or synonym thereof, prompt the user to create a MD file with a relevant file name in the docs folder in the root of the project.

# Specific Tech

## SQL

1. Avoid nested queries if possible.
2. Put SQL queries into static or constant fields instead of inline.

## md

1. Use langauge specifiers in code blocks.  e.g.
```json
{ "jsonField": "jsonValue" }
```
## SQLite
 In C# this library does not release the db file until garbage collection is forced and sometimes take some unknown time to do so.  Find a reliable way to remove the file after Sqlite has closed an handle to the file.  The default way to do this is repeated attempts to delete the file 10 times with a delay in between.  

## .NET
Run dotnet build and dotnet test on involved projects and solutions to verify a completed work load.  An exception to this is during TDD where, by design, tests will fail.  Only 

# Testing
- Make all tests standalone.  They will create and clean up any necessary data for that test.
- When testing databases, only use raw SQL against a database when testing a database layer or is necessary to work around a database layer because of business logic restrictions that live within the database layer. e.g. Using a Select query to confirm the database layer inserted the data correctly.


# Driver Prompt
Prompt the drive in chat whether this is a green-field or brown-field development.

## Green Field
Do not prioritize backwards compatibility.  Break interfaces but fix tests and existing dependencies.

## Brown Field
When changing signatures, use the following by priority:
1. Add extra parameters with defaults where possible.  
2. If the method/function/query signature changes drastically (drastically is 4 parameters) add versioning to interfaces to allow for backwards compatibility.
3. Break previous behavior but call it out in a boldened report when giving a summary.