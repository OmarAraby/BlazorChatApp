window.scrollToBottom = function (element) {
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
};

window.copyToClipboard = function (text) {
    navigator.clipboard.writeText(text)
        .then(() => {
            return true;
        })
        .catch(() => {
            return false;
        });
};

window.highlightCode = function () {
    if (typeof Prism !== 'undefined') {
        Prism.highlightAll();
    }
};

window.playNotificationSound = function () {
    const audio = new Audio('/sounds/notification.mp3');
    audio.play().catch(e => console.error('Error playing notification sound:', e));
};

window.registerServiceWorker = function () {
    if ('serviceWorker' in navigator) {
        navigator.serviceWorker.register('/service-worker.js')
            .then(registration => {
                console.log('ServiceWorker registration successful with scope: ', registration.scope);
            })
            .catch(err => {
                console.log('ServiceWorker registration failed: ', err);
            });
    }
};

window.requestNotificationPermission = function () {
    if ('Notification' in window) {
        return Notification.requestPermission();
    }
    return Promise.resolve('denied');
};

window.showNotification = function (title, body, icon) {
    if ('Notification' in window && Notification.permission === 'granted') {
        const notification = new Notification(title, {
            body: body,
            icon: icon || '/images/logo.png'
        });

        notification.onclick = function () {
            window.focus();
            notification.close();
        };

        return true;
    }
    return false;
};

window.getThemePreference = function () {
    return localStorage.getItem('theme') || 'light';
};

window.setThemePreference = function (theme) {
    localStorage.setItem('theme', theme);
    return true;
};

window.detectMobile = function () {
    return /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent);
};

