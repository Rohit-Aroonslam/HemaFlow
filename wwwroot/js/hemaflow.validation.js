/**
 * hemaflow.validation.js
 * Shared client-side validation for HemaFlow
 * Works alongside ASP.NET Core Unobtrusive Validation.
 * Drop this file in wwwroot/js/ and reference it after jquery.validate.unobtrusive.min.js
 */

(function () {
    'use strict';

    // ── Helpers ──────────────────────────────────────────────────────────────

    function showError(input, message) {
        input.classList.add('is-invalid');
        input.classList.remove('is-valid');
        let span = input.parentElement.querySelector('.hf-val-msg');
        if (!span) {
            span = document.createElement('div');
            span.className = 'hf-val-msg invalid-feedback';
            input.parentElement.appendChild(span);
        }
        span.textContent = message;
    }

    function clearError(input) {
        input.classList.remove('is-invalid');
        input.classList.add('is-valid');
        const span = input.parentElement.querySelector('.hf-val-msg');
        if (span) span.textContent = '';
    }

    function clearAll(input) {
        input.classList.remove('is-invalid', 'is-valid');
        const span = input.parentElement.querySelector('.hf-val-msg');
        if (span) span.textContent = '';
    }

    // ── Validators ───────────────────────────────────────────────────────────

    const validators = {

        required(value, label) {
            if (!value || value.trim() === '')
                return `${label} is required.`;
            return null;
        },

        minLength(value, min, label) {
            if (value && value.trim().length < min)
                return `${label} must be at least ${min} characters.`;
            return null;
        },

        maxLength(value, max, label) {
            if (value && value.trim().length > max)
                return `${label} cannot exceed ${max} characters.`;
            return null;
        },

        saId(value) {
            if (!value) return null;
            const clean = value.replace(/\s/g, '');
            if (!/^\d{13}$/.test(clean))
                return 'SA ID number must be exactly 13 digits.';
            // Luhn check
            let sum = 0;
            for (let i = 0; i < 13; i++) {
                let d = parseInt(clean[i]);
                if (i % 2 === 1) { d *= 2; if (d > 9) d -= 9; }
                sum += d;
            }
            if (sum % 10 !== 0)
                return 'SA ID number is invalid (failed check digit).';
            return null;
        },

        hpcsa(value) {
            if (!value) return null;
            if (!/^MP\d{6,8}$/i.test(value.trim()))
                return 'HPCSA number must start with MP followed by 6–8 digits (e.g. MP0491823).';
            return null;
        },

        email(value) {
            if (!value) return null;
            if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value.trim()))
                return 'Please enter a valid email address.';
            return null;
        },

        phone(value) {
            if (!value) return null;
            const clean = value.replace(/[\s\-().]/g, '');
            if (!/^(\+27|0)[0-9]{9}$/.test(clean))
                return 'Please enter a valid South African phone number (e.g. +27 81 028 6437 or 081 028 6437).';
            return null;
        },

        password(value) {
            if (!value) return 'Password is required.';
            if (value.length < 8) return 'Password must be at least 8 characters.';
            if (!/[A-Z]/.test(value)) return 'Password must contain at least one uppercase letter.';
            if (!/[0-9]/.test(value)) return 'Password must contain at least one number.';
            if (!/[^A-Za-z0-9]/.test(value)) return 'Password must contain at least one special character.';
            return null;
        },

        icd10(value) {
            if (!value) return null;
            if (!/^[A-Z][0-9]{2}(\.[0-9]{1,4})?$/i.test(value.trim()))
                return 'ICD-10 code must be in the format: A00 or A00.0';
            return null;
        },

        dateRange(from, to) {
            if (!from || !to) return null;
            if (new Date(to) < new Date(from))
                return 'To date cannot be before the From date.';
            return null;
        },

        positiveNumber(value, label) {
            if (!value) return null;
            if (isNaN(value) || Number(value) <= 0)
                return `${label} must be a positive number.`;
            return null;
        }
    };

    // ── Attach live validation to a field ─────────────────────────────────────

    function attachLive(input, validateFn) {
        // Validate on blur
        input.addEventListener('blur', function () {
            const err = validateFn(this.value);
            err ? showError(this, err) : clearError(this);
        });
        // Clear error state while typing
        input.addEventListener('input', function () {
            if (this.classList.contains('is-invalid')) {
                const err = validateFn(this.value);
                err ? showError(this, err) : clearError(this);
            }
        });
        // Clear pristine state on first focus
        input.addEventListener('focus', function () {
            if (!this.classList.contains('is-invalid') && !this.classList.contains('is-valid'))
                clearAll(this);
        });
    }

    // ── Auto-wire by data-hf-* attributes ─────────────────────────────────────
    //
    // Add these attributes to your inputs:
    //   data-hf-required        → field is required
    //   data-hf-sa-id           → validate as SA ID
    //   data-hf-hpcsa           → validate as HPCSA number
    //   data-hf-email           → validate as email
    //   data-hf-phone           → validate as SA phone
    //   data-hf-password        → validate as password
    //   data-hf-icd10           → validate as ICD-10 code
    //   data-hf-min="N"         → min length
    //   data-hf-max="N"         → max length
    //   data-hf-label="Label"   → field label for error messages

    document.addEventListener('DOMContentLoaded', function () {

        document.querySelectorAll('[data-hf-required], [data-hf-sa-id], [data-hf-hpcsa], [data-hf-email], [data-hf-phone], [data-hf-password], [data-hf-icd10]')
            .forEach(function (input) {
                const label = input.dataset.hfLabel || input.placeholder || 'This field';

                attachLive(input, function (value) {
                    // Required check
                    if (input.hasAttribute('data-hf-required')) {
                        const err = validators.required(value, label);
                        if (err) return err;
                    }

                    // Type-specific checks
                    if (input.hasAttribute('data-hf-sa-id')) { const e = validators.saId(value); if (e) return e; }
                    if (input.hasAttribute('data-hf-hpcsa')) { const e = validators.hpcsa(value); if (e) return e; }
                    if (input.hasAttribute('data-hf-email')) { const e = validators.email(value); if (e) return e; }
                    if (input.hasAttribute('data-hf-phone')) { const e = validators.phone(value); if (e) return e; }
                    if (input.hasAttribute('data-hf-password')) { const e = validators.password(value); if (e) return e; }
                    if (input.hasAttribute('data-hf-icd10')) { const e = validators.icd10(value); if (e) return e; }

                    // Min/max length
                    if (input.dataset.hfMin) { const e = validators.minLength(value, parseInt(input.dataset.hfMin), label); if (e) return e; }
                    if (input.dataset.hfMax) { const e = validators.maxLength(value, parseInt(input.dataset.hfMax), label); if (e) return e; }

                    return null;
                });
            });

        // Date range pairs: data-hf-date-from="fieldId" on the To field
        document.querySelectorAll('[data-hf-date-to]').forEach(function (toInput) {
            const fromId = toInput.dataset.hfDateTo;
            const fromInput = document.getElementById(fromId);
            if (!fromInput) return;
            [toInput, fromInput].forEach(function (el) {
                el.addEventListener('change', function () {
                    const err = validators.dateRange(fromInput.value, toInput.value);
                    err ? showError(toInput, err) : clearError(toInput);
                });
            });
        });

        // ── Whole-form validation on submit ──────────────────────────────────
        document.querySelectorAll('form[data-hf-validate]').forEach(function (form) {
            form.addEventListener('submit', function (e) {
                let valid = true;
                form.querySelectorAll('[data-hf-required]').forEach(function (input) {
                    const label = input.dataset.hfLabel || 'This field';
                    const err = validators.required(input.value, label);
                    if (err) { showError(input, err); valid = false; }
                });
                if (!valid) {
                    e.preventDefault();
                    // Scroll to first error
                    const first = form.querySelector('.is-invalid');
                    if (first) first.scrollIntoView({ behavior: 'smooth', block: 'center' });
                }
            });
        });

        // ── Password strength indicator ───────────────────────────────────────
        document.querySelectorAll('[data-hf-password]').forEach(function (input) {
            const wrapper = document.createElement('div');
            wrapper.className = 'hf-pw-strength mt-1';
            wrapper.innerHTML = `
                <div class="d-flex gap-1 mb-1">
                    <div class="hf-pw-bar flex-fill rounded-0" style="height:3px;background:#e2e8f0;" id="${input.id}_b1"></div>
                    <div class="hf-pw-bar flex-fill rounded-0" style="height:3px;background:#e2e8f0;" id="${input.id}_b2"></div>
                    <div class="hf-pw-bar flex-fill rounded-0" style="height:3px;background:#e2e8f0;" id="${input.id}_b3"></div>
                    <div class="hf-pw-bar flex-fill rounded-0" style="height:3px;background:#e2e8f0;" id="${input.id}_b4"></div>
                </div>
                <div id="${input.id}_label" style="font-size:0.68rem;color:#64748b;"></div>`;
            input.parentElement.appendChild(wrapper);

            input.addEventListener('input', function () {
                const v = this.value;
                let score = 0;
                if (v.length >= 8) score++;
                if (/[A-Z]/.test(v)) score++;
                if (/[0-9]/.test(v)) score++;
                if (/[^A-Za-z0-9]/.test(v)) score++;

                const colors = ['#ef4444', '#f97316', '#eab308', '#22c55e'];
                const labels = ['Weak', 'Fair', 'Good', 'Strong'];
                const bars = [1, 2, 3, 4];
                bars.forEach(function (n) {
                    const bar = document.getElementById(`${input.id}_b${n}`);
                    bar.style.background = n <= score ? colors[score - 1] : '#e2e8f0';
                });
                const lbl = document.getElementById(`${input.id}_label`);
                lbl.textContent = v.length ? labels[score - 1] || '' : '';
                lbl.style.color = score > 0 ? colors[score - 1] : '#64748b';
            });
        });

        // ── Toggle password visibility ────────────────────────────────────────
        document.querySelectorAll('[data-hf-toggle-pw]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                const targetId = this.dataset.hfTogglePw;
                const input = document.getElementById(targetId);
                if (!input) return;
                const isText = input.type === 'text';
                input.type = isText ? 'password' : 'text';
                const icon = this.querySelector('i');
                if (icon) {
                    icon.className = isText ? 'bi bi-eye' : 'bi bi-eye-slash';
                }
            });
        });
    });

    // Expose validators globally for custom use
    window.HFValidators = validators;

})();
