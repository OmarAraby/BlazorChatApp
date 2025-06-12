# 💬 RealTimeChat Application

Welcome to **RealTimeChat**, a modern real-time chat application built with **Blazor Server**, **SignalR**, and **ASP.NET Core**. It allows users to chat instantly in real-time through public or private rooms, with features like invitations, notifications, and identity management.

## 🧭 Overview

RealTimeChat aims to facilitate seamless and secure communication through dynamic chat rooms. It leverages the power of Blazor for responsive UI, SignalR for real-time data transfer, and ASP.NET Core Identity for robust authentication and user management.BlazorChatApp

Real-time chat application built using **Blazor Server**, **SignalR**, and **ASP.NET Core Identity**.  
It supports **user registration**, **authentication**, and **protected SignalR-based chat** between users in real time.

---

## ✨ Features

- 🔁 **Real-Time Messaging** using SignalR
    
- 🌍 **Public & Private Chat Rooms**
    
- 🔐 **User Authentication** with ASP.NET Core Identity
    
- 📬 **User Invitations** to join private rooms
    
- 🔔 **Real-Time Notifications** for invitations and messages
    
- 🧾 **Room Management** (Create, Join, Leave)
    
- 📱 **Responsive UI** using [MudBlazor](https://mudblazor.com/)
    
- 🔄 **Auto-Refresh** on room changes
    
- 🔎 **Room Search Dialog** with integrated filtering
    
---
## 🛠️ Technologies Used

| Layer        | Tech Stack                         |
| ------------ | ---------------------------------- |
| **Frontend** | Blazor Server + MudBlazor          |
| **Backend**  | ASP.NET Core 8.0                   |
| **Realtime** | SignalR                            |
| **Database** | Entity Framework Core + SQL Server |
| **Auth**     | ASP.NET Core Identity              |
| **IDE**      | Visual Studio 2022+                |


## 📸 Screenshots!
![image](https://github.com/user-attachments/assets/8d526449-7cbc-4c6e-b13b-e7a791b067b6)
![image](https://github.com/user-attachments/assets/59e01206-e9e8-45d4-a79c-bef240fe3775)
![image](https://github.com/user-attachments/assets/93023a94-60f7-4c5a-aa6f-1d2bbb153d6c)
![image](https://github.com/user-attachments/assets/2a079326-02d5-4aeb-a9b4-96b7ce78cf66)
![image](https://github.com/user-attachments/assets/fdb19f4d-15eb-4c31-bccc-2cad03db3ee5)


---
## 🚀 Getting Started

### ✅ Prerequisites

- [.NET SDK 8.0](https://dotnet.microsoft.com/download)
    
- SQL Server (LocalDB or full instance)
    
- Visual Studio 2022 or later (with ASP.NET and web workload)
    

### 📦 Installation

1. **Clone the Repository**
    
    ```bash
    git clone https://github.com/OmarAraby/BlazorChatApp.git
    cd BlazorChatApp
    ```
    
2. **Configure the Database**
    
    - Edit `appsettings.json`:
        
        ```json
        "ConnectionStrings": {
          "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=RealTimeChatDb;Trusted_Connection=True;MultipleActiveResultSets=true"
        }
        ```
        
    - Run migrations:
        
        ```bash
        dotnet ef migrations add InitialCreate
        dotnet ef database update
        ```
        
3. **Restore & Run**
    
    ```bash
    dotnet restore
    dotnet run
    ```
    
    Open your browser at: `https://localhost:7118`
    

## 🧪 Usage

- **Register/Login** to access chat features
    
- **Create Room** (public/private)
    
- **Join Room** directly or via invitation
    
- **Chat in Real-Time**
    
- **Invite Users** to private rooms
  ---

## 🧠 Project Structure

```
BlazorChatApp/
│
├── Components/          # Reusable UI components
      └── Pages/               # Razor Pages (Login, Register, Chat)
├── Data/                # ApplicationDbContext & seeding
├── Hubs/                # SignalR ChatHub
├── Models/              # Application models (e.g., ChatMessage, ConnectedUser)
├── Services/            # Business logic & helper services
├── wwwroot/             # Static files (JS, CSS, images)
```

---

## 🧪 Future Enhancements

- 📱 Mobile responsiveness

- 📝 Chat history persistence
    
- 🔔 Notifications & typing indicators
    
- 📬 Private messaging
    

---

## 🤝 Contributing

Contributions are welcome!  
Feel free to open issues, suggest enhancements, or submit pull requests.

---

## 📄 License

This project is open-source and available under the [MIT License](https://chatgpt.com/c/LICENSE).

---

## 🙋‍♂️ Author

**Omar Araby**  
[GitHub](https://github.com/OmarAraby) • [LinkedIn](https://www.linkedin.com/in/omar-araby)

