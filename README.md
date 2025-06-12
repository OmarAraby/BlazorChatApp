# 💬 BlazorChatApp

Real-time chat application built using **Blazor Server**, **SignalR**, and **ASP.NET Core Identity**.  
It supports **user registration**, **authentication**, and **protected SignalR-based chat** between users in real time.

---

## 🚀 Features

- 🔐 User authentication and registration (ASP.NET Core Identity)
- ⚡ Real-time messaging using SignalR
- 🔒 Secured chat hub with `[Authorize]` attribute
- 🧑‍🤝‍🧑 Track connected users
- 🌙 Light/Dark theme toggle
- 🧩 Modular architecture (UI components, services, models)

---

## 📸 Screenshots!
![image](https://github.com/user-attachments/assets/8d526449-7cbc-4c6e-b13b-e7a791b067b6)
![image](https://github.com/user-attachments/assets/59e01206-e9e8-45d4-a79c-bef240fe3775)
![image](https://github.com/user-attachments/assets/93023a94-60f7-4c5a-aa6f-1d2bbb153d6c)
![image](https://github.com/user-attachments/assets/2a079326-02d5-4aeb-a9b4-96b7ce78cf66)
![image](https://github.com/user-attachments/assets/fdb19f4d-15eb-4c31-bccc-2cad03db3ee5)

## 🛠️ Technologies Used

- [.NET 8](https://dotnet.microsoft.com/en-us/)
- [Blazor Server](https://learn.microsoft.com/en-us/aspnet/core/blazor/)
- [SignalR](https://learn.microsoft.com/en-us/aspnet/core/signalr/)
- [ASP.NET Core Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity)
- [MudBlazor (UI framework)](https://mudblazor.com/) – for material design components

---

## 📦 Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- Visual Studio 2022+ or VS Code

### Run the project

1. **Clone the repository**

   ```bash
   git clone https://github.com/OmarAraby/BlazorChatApp.git
   cd BlazorChatApp
   ```
   
2. **Apply Migrations & Update Database**
    
    ```bash
    dotnet ef database update
    ```
    
3. **Run the application**
    
    ```bash
    dotnet run
    ```
    
4. Visit `https://localhost:7118` in your browser.
    

---

## 👤 Default Roles / Access

- New users can register and chat instantly.
    
- Only authenticated users can access the chat hub (`[Authorize]`).
    

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

