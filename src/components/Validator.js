import { VALIDATION_CONFIG } from '../config/constants.js';

export class Validator {
    constructor() {
        this.rules = {
            email: {
                validate: (value) => VALIDATION_CONFIG.email.pattern.test(value),
                message: VALIDATION_CONFIG.email.message
            },
            required: {
                validate: VALIDATION_CONFIG.required.validate,
                message: VALIDATION_CONFIG.required.message
            },
            minLength: (min) => ({
                validate: (value) => value && value.length >= min,
                message: `Минимальная длина: ${min} символов`
            }),
            maxLength: (max) => ({
                validate: (value) => !value || value.length <= max,
                message: `Максимальная длина: ${max} символов`
            }),
            password: {
                validate: (value) => VALIDATION_CONFIG.password.pattern.test(value),
                message: VALIDATION_CONFIG.password.message
            },
            phone: {
                validate: (value) => !value || /^8\d{10}$/.test(value.replace(/\D/g, '')),
                message: "Номер телефона должен начинаться с 8 и содержать 11 цифр"
            },
            code: {
                validate: (value) => VALIDATION_CONFIG.code.pattern.test(value),
                message: VALIDATION_CONFIG.code.message
            },
            username: {
                validate: (value) => VALIDATION_CONFIG.username.pattern.test(value),
                message: VALIDATION_CONFIG.username.message
            }
        };
    }

    validate(field, value, rules) {
        const errors = [];
        
        for (const rule of rules) {
            if (!rule.validate(value)) {
                errors.push(rule.message);
                break;
            }
        }
        
        return errors;
    }

    createRule(ruleName, param = null) {
        if (typeof this.rules[ruleName] === 'function') {
            return this.rules[ruleName](param);
        }
        return this.rules[ruleName];
    }

    validateForm(formId, formData) {
        const config = this.getValidationConfig(formId);
        if (!config) return { isValid: true, errors: {} };

        const errors = {};
        let isValid = true;

        for (const [fieldId, rules] of Object.entries(config.fields)) {
            const value = formData[fieldId] || '';
            const fieldErrors = this.validate(fieldId, value, rules);
            
            if (fieldErrors.length > 0) {
                errors[fieldId] = fieldErrors[0];
                isValid = false;
            }
        }

        if (config.custom && isValid) {
            const customErrors = config.custom(formData);
            customErrors.forEach(error => {
                errors[error.field] = error.message;
                isValid = false;
            });
        }

        return { isValid, errors };
    }

    getValidationConfig(formId) {
        const configs = {
            'form-login': {
                fields: {
                    'login-email': [
                        this.createRule('required'),
                        this.createRule('email')
                    ],
                    'login-password': [
                        this.createRule('required'),
                        this.createRule('minLength', 6)
                    ]
                }
            },
            'form-signup': {
                fields: {
                    'signup-username': [
                        this.createRule('required'),
                        this.createRule('username')
                    ],
                    'signup-email': [
                        this.createRule('required'),
                        this.createRule('email')
                    ],
                    'signup-phone': [
                        this.createRule('phone')
                    ],
                    'signup-password': [
                        this.createRule('required'),
                        this.createRule('password')
                    ],
                    'signup-password-confirm': [
                        this.createRule('required')
                    ]
                },
                custom: (formData) => {
                    const errors = [];
                    if (formData['signup-password'] !== formData['signup-password-confirm']) {
                        errors.push({ field: 'signup-password-confirm', message: 'Пароли не совпадают' });
                    }
                    return errors;
                }
            },
            'form-forgot-password': {
                fields: {
                    'forgot-email': [
                        this.createRule('required'),
                        this.createRule('email')
                    ]
                }
            },
            'form-reset-code': {
                fields: {
                    'reset-code': [
                        this.createRule('required'),
                        this.createRule('code')
                    ]
                }
            },
            'form-new-password': {
                fields: {
                    'new-password': [
                        this.createRule('required'),
                        this.createRule('password')
                    ],
                    'confirm-new-password': [
                        this.createRule('required')
                    ]
                },
                custom: (formData) => {
                    const errors = [];
                    if (formData['new-password'] !== formData['confirm-new-password']) {
                        errors.push({ field: 'confirm-new-password', message: 'Пароли не совпадают' });
                    }
                    return errors;
                }
            },
            'change-password-form': {
                fields: {
                    'current-password': [
                        this.createRule('required')
                    ],
                    'change-new-password': [
                        this.createRule('required'),
                        this.createRule('password')
                    ],
                    'change-confirm-password': [
                        this.createRule('required')
                    ]
                },
                custom: (formData) => {
                    const errors = [];
                    if (formData['change-new-password'] !== formData['change-confirm-password']) {
                        errors.push({ field: 'change-confirm-password', message: 'Пароли не совпадают' });
                    }
                    return errors;
                }
            }
        };

        return configs[formId];
    }
}