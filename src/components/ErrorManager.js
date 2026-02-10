export class ErrorManager {
    constructor() {
        this.errorElements = new Map();
    }

    showError(fieldId, message) {
        this.clearError(fieldId);
        
        const field = document.getElementById(fieldId);
        if (!field) return;

        const errorElement = document.createElement('div');
        errorElement.className = 'field-error';
        errorElement.textContent = message;
        errorElement.id = `error-${fieldId}`;

        field.classList.add('field-error');
        field.parentNode.appendChild(errorElement);
        
        this.errorElements.set(fieldId, errorElement);
        this.showErrorIcon(fieldId);
    }

    clearError(fieldId) {
        const errorElement = document.getElementById(`error-${fieldId}`);
        if (errorElement) {
            errorElement.remove();
        }
        
        const field = document.getElementById(fieldId);
        if (field) {
            field.classList.remove('field-error');
        }
        
        this.hideErrorIcon(fieldId);
        this.errorElements.delete(fieldId);
    }

    clearAllErrors() {
        for (const [fieldId] of this.errorElements) {
            this.clearError(fieldId);
        }
        this.errorElements.clear();
    }

    showErrorIcon(fieldId) {
        const field = document.getElementById(fieldId);
        if (!field) return;
        
        let icon = field.parentNode.querySelector('.field-status-icon');
        if (!icon) {
            icon = document.createElement('span');
            icon.className = 'field-status-icon error';
            icon.textContent = '';
            field.parentNode.appendChild(icon);
        }
    }

    hideErrorIcon(fieldId) {
        const field = document.getElementById(fieldId);
        if (!field) return;
        
        const icon = field.parentNode.querySelector('.field-status-icon');
        if (icon) {
            icon.remove();
        }
    }

    showSuccessIcon(fieldId) {
        const field = document.getElementById(fieldId);
        if (!field) return;
        
        let icon = field.parentNode.querySelector('.field-status-icon');
        if (!icon) {
            icon = document.createElement('span');
            icon.className = 'field-status-icon success';
            icon.textContent = '';
            field.parentNode.appendChild(icon);
        } else {
            icon.className = 'field-status-icon success';
            icon.textContent = '';
        }
    }

    setupLiveValidation(formId, validator) {
        const config = validator.getValidationConfig(formId);
        if (!config) return;

        for (const fieldId of Object.keys(config.fields)) {
            const field = document.getElementById(fieldId);
            if (field) {
                field.addEventListener('blur', () => {
                    this.validateField(fieldId, formId, validator);
                });
                
                field.addEventListener('input', () => {
                    if (field.value.trim()) {
                        this.clearError(fieldId);
                        this.hideErrorIcon(fieldId);
                    }
                });
            }
        }
    }

    validateField(fieldId, formId, validator) {
        const config = validator.getValidationConfig(formId);
        if (!config || !config.fields[fieldId]) return;

        const field = document.getElementById(fieldId);
        if (!field) return;

        const value = field.value.trim();
        const rules = config.fields[fieldId];
        const errors = validator.validate(fieldId, value, rules);

        if (errors.length > 0) {
            this.showError(fieldId, errors[0]);
            return false;
        } else {
            this.clearError(fieldId);
            this.showSuccessIcon(fieldId);
            return true;
        }
    }

    showFormErrors(errors) {
        this.clearAllErrors();
        Object.entries(errors).forEach(([fieldId, message]) => {
            this.showError(fieldId, message);
        });
    }
}