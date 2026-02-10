export const API_URL = "https://localhost:7000/api";

export const VALIDATION_CONFIG = {
    email: {
        pattern: /^[^\s@]+@[^\s@]+\.[^\s@]+$/,
        message: "Введите корректный email адрес"
    },
    required: {
        validate: (value) => value && value.trim().length > 0,
        message: "Это поле обязательно для заполнения"
    },
    password: {
        pattern: /^(?=.*[A-Za-z])(?=.*\d).{8,}$/,
        message: "Пароль должен содержать минимум 8 символов, буквы и цифры"
    },
    username: {
        pattern: /^[a-zA-Zа-яА-Я0-9_\-\s]{2,30}$/,
        message: "Имя пользователя должно содержать 2-30 символов"
    },
    phone: {
        pattern: /^8\d{10}$/,
        message: "Номер телефона должен начинаться с 8 и содержать 11 цифр"
    },
    code: {
        pattern: /^\d{6}$/,
        message: "Код должен состоять из 6 цифр"
    }
};

export const FORM_CONFIGS = {
    login: {
        fields: ['login-email', 'login-password']
    },
    signup: {
        fields: ['signup-username', 'signup-email', 'signup-password', 'signup-password-confirm']
    },
    forgotPassword: {
        fields: ['forgot-email']
    },
    resetCode: {
        fields: ['reset-code']
    },
    newPassword: {
        fields: ['new-password', 'confirm-new-password']
    },
    changePassword: {
        fields: ['current-password', 'change-new-password', 'change-confirm-password']
    }
};