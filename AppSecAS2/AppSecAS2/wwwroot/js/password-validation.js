// Password Policy Validation - Client-side validation that mirrors server-side rules
(function () {
    'use strict';

    // Password policy constants (must match PasswordPolicyService.cs)
    const PASSWORD_RULES = {
        minLength: 12,
        patterns: {
            lowercase: /[a-z]/,
            uppercase: /[A-Z]/,
            digit: /\d/,
            special: /[^a-zA-Z0-9]/
        }
    };

    // Initialize password validation on page load
    document.addEventListener('DOMContentLoaded', function () {
        const passwordFields = document.querySelectorAll('[data-password-validation]');
        passwordFields.forEach(function (field) {
            initializePasswordValidation(field);
        });
    });

    function initializePasswordValidation(passwordField) {
        const feedbackContainer = createFeedbackContainer();
        passwordField.parentNode.insertBefore(feedbackContainer, passwordField.nextSibling);

        // Add input event listener for real-time validation
        passwordField.addEventListener('input', function () {
            validatePasswordRealTime(passwordField.value, feedbackContainer);
        });

        // Validate on blur as well
        passwordField.addEventListener('blur', function () {
            validatePasswordRealTime(passwordField.value, feedbackContainer);
        });
    }

    function createFeedbackContainer() {
        const container = document.createElement('div');
        container.className = 'password-validation-feedback mt-2';
        container.innerHTML = `
            <div class="password-strength mb-2">
                <span class="strength-label">Password Strength: </span>
                <span class="strength-value badge bg-secondary">Not Set</span>
            </div>
            <ul class="password-rules list-unstyled mb-0">
                <li data-rule="length">
                    <i class="rule-icon bi bi-circle"></i>
                    <span class="rule-text">At least ${PASSWORD_RULES.minLength} characters</span>
                </li>
                <li data-rule="lowercase">
                    <i class="rule-icon bi bi-circle"></i>
                    <span class="rule-text">At least one lowercase letter (a-z)</span>
                </li>
                <li data-rule="uppercase">
                    <i class="rule-icon bi bi-circle"></i>
                    <span class="rule-text">At least one uppercase letter (A-Z)</span>
                </li>
                <li data-rule="digit">
                    <i class="rule-icon bi bi-circle"></i>
                    <span class="rule-text">At least one digit (0-9)</span>
                </li>
                <li data-rule="special">
                    <i class="rule-icon bi bi-circle"></i>
                    <span class="rule-text">At least one special character (!@#$%^&*...)</span>
                </li>
            </ul>
        `;
        return container;
    }

    function validatePasswordRealTime(password, feedbackContainer) {
        if (!password) {
            resetFeedback(feedbackContainer);
            return;
        }

        const validation = validatePassword(password);
        updateFeedbackUI(feedbackContainer, validation);
    }

    function validatePassword(password) {
        const result = {
            rules: {
                length: password.length >= PASSWORD_RULES.minLength,
                lowercase: PASSWORD_RULES.patterns.lowercase.test(password),
                uppercase: PASSWORD_RULES.patterns.uppercase.test(password),
                digit: PASSWORD_RULES.patterns.digit.test(password),
                special: PASSWORD_RULES.patterns.special.test(password)
            },
            isValid: false,
            strength: 'Very Weak',
            rulesSatisfied: 0
        };

        // Count satisfied rules
        result.rulesSatisfied = Object.values(result.rules).filter(Boolean).length;
        result.isValid = result.rulesSatisfied === 5;

        // Calculate strength
        result.strength = getPasswordStrength(result.rulesSatisfied);

        return result;
    }

    function getPasswordStrength(rulesSatisfied) {
        switch (rulesSatisfied) {
            case 5:
                return 'Strong';
            case 4:
                return 'Medium';
            case 3:
                return 'Weak';
            default:
                return 'Very Weak';
        }
    }

    function updateFeedbackUI(container, validation) {
        // Update strength badge
        const strengthBadge = container.querySelector('.strength-value');
        strengthBadge.textContent = validation.strength;
        strengthBadge.className = 'strength-value badge ' + getStrengthBadgeClass(validation.strength);

        // Update each rule
        Object.keys(validation.rules).forEach(function (ruleKey) {
            const ruleElement = container.querySelector(`[data-rule="${ruleKey}"]`);
            const icon = ruleElement.querySelector('.rule-icon');
            const isPassed = validation.rules[ruleKey];

            if (isPassed) {
                ruleElement.classList.remove('text-danger');
                ruleElement.classList.add('text-success');
                icon.className = 'rule-icon bi bi-check-circle-fill';
            } else {
                ruleElement.classList.remove('text-success');
                ruleElement.classList.add('text-danger');
                icon.className = 'rule-icon bi bi-x-circle-fill';
            }
        });
    }

    function resetFeedback(container) {
        const strengthBadge = container.querySelector('.strength-value');
        strengthBadge.textContent = 'Not Set';
        strengthBadge.className = 'strength-value badge bg-secondary';

        const rules = container.querySelectorAll('[data-rule]');
        rules.forEach(function (rule) {
            rule.classList.remove('text-success', 'text-danger');
            const icon = rule.querySelector('.rule-icon');
            icon.className = 'rule-icon bi bi-circle';
        });
    }

    function getStrengthBadgeClass(strength) {
        switch (strength) {
            case 'Strong':
                return 'bg-success';
            case 'Medium':
                return 'bg-warning';
            case 'Weak':
                return 'bg-orange';
            default:
                return 'bg-danger';
        }
    }

    // Expose validation function for external use if needed
    window.PasswordValidator = {
        validate: validatePassword,
        rules: PASSWORD_RULES
    };
})();
