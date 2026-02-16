import { Validators } from '@angular/forms';

export const VALIDATION = {
  customerName: { minLength: 1, maxLength: 200 },
  email: { maxLength: 256 },
  countryCode: { length: 2 },
  currencyCode: { length: 3 },
  productSku: { minLength: 1, maxLength: 200 },
  productName: { minLength: 1, maxLength: 200 },
  productId: { required: true },
  quantity: { min: 1 },
  unitPrice: { min: 0 },
} as const;

export function customerNameValidators() {
  return [
    Validators.required,
    Validators.minLength(VALIDATION.customerName.minLength),
    Validators.maxLength(VALIDATION.customerName.maxLength),
  ];
}

export function emailValidators() {
  return [Validators.email, Validators.maxLength(VALIDATION.email.maxLength)];
}

export function countryCodeValidators() {
  return [
    Validators.required,
    Validators.minLength(VALIDATION.countryCode.length),
    Validators.maxLength(VALIDATION.countryCode.length),
  ];
}

export function currencyCodeValidators() {
  return [
    Validators.required,
    Validators.minLength(VALIDATION.currencyCode.length),
    Validators.maxLength(VALIDATION.currencyCode.length),
  ];
}

export function productSkuValidators() {
  return [
    Validators.required,
    Validators.minLength(VALIDATION.productSku.minLength),
    Validators.maxLength(VALIDATION.productSku.maxLength),
  ];
}

export function productNameValidators() {
  return [
    Validators.required,
    Validators.minLength(VALIDATION.productName.minLength),
    Validators.maxLength(VALIDATION.productName.maxLength),
  ];
}

export function quantityValidators() {
  return [Validators.required, Validators.min(VALIDATION.quantity.min)];
}

export function unitPriceValidators() {
  return [Validators.required, Validators.min(VALIDATION.unitPrice.min)];
}
