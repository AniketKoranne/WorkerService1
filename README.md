📌 Build and Deploy as a Windows Service

1️⃣ Build the Project
Click Build > Build Solution (Ctrl + Shift + B).

2️⃣ Publish the Application
Right-click the project → Publish.

Choose Folder as the target.

Publish to C:\EmployeeApiWorker.

3️⃣ Install the Windows Service
Open Command Prompt as Administrator.

Run this command to install the service:

sh
Copy
Edit
sc create EmployeeApiWorker binPath= "C:\EmployeeApiWorker\EmployeeApiWorker.exe"
Start the service:

sh
Copy
Edit
sc start EmployeeApiWorker
4️⃣ Verify the Service
Open Services (services.msc) and find EmployeeApiWorker.

Check logs in Event Viewer under Windows Logs → Application.
