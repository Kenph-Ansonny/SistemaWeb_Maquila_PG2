/*
// Expresiones Regulares Centralizadas
export const Patterns = {
    ONLY_NUMBERS: /^[0-9]+$/,
    DECIMAL: /^\d+(\.\d{1,4})?$/,
    ALPHANUMERIC: /^[a-zA-Z0-9\sñÑáéíóúÁÉÍÓÚ]+$/,
    EMAIL: /^[^\s@]+@[^\s@]+\.[^\s@]+$/,
    PHONE: /^[+]*[(]{0,1}[0-9]{1,4}[)]{0,1}[-\s./0-9]*$/
  }
  
  // Filtros de eventos de teclado (Previene que el usuario escriba caracteres inválidos en tiempo real)
  export const allowOnly = {
    numbers(event) {
      if (!/[0-9]/.test(event.key)) event.preventDefault()
    },
    decimals(event, currentValue = '') {
      if (!/[0-9.]/.test(event.key)) event.preventDefault()
      if (event.key === '.' && currentValue.includes('.')) event.preventDefault()
    },
    cleanText(event) {
      // Bloquea caracteres especiales potencialmente peligrosos (<, >, {, }, ;, ', ")
      if (/[<>{};'"]/.test(event.key)) event.preventDefault()
    }
  }
  
  // Funciones de comprobación para usar al enviar formularios
  export const isValid = {
    email: (val) => Patterns.EMAIL.test(val),
    required: (val) => val !== null && val !== undefined && val.toString().trim().length > 0,
    minLength: (val, min) => val && val.length >= min
  }
    */

  export const allowOnly = {
    usuarioInput(event) {
      // Permite letras, números, guiones, puntos, espacios, @ y !
      const regex = /^[a-zA-Z0-9_\-@!.\s]$/
      if (!regex.test(event.key)) {
        event.preventDefault()
      }
    }
  }
  
  export const EmailRules = {
    validate(email) {
      if (!email) return { isValid: false, hasAt: false, hasDomain: false, noSpaces: false }
      const hasAt = email.includes('@')
      const hasDomain = /@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/.test(email)
      const noSpaces = !/\s/.test(email)
      const isValid = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)
  
      return { hasAt, hasDomain, noSpaces, isValid }
    }
  }
  
  export const PasswordRules = {
    validate(password) {
      if (!password) return { isValid: false }
  
      const minLength = password.length >= 8
      const hasUpper = /[A-Z]/.test(password)
      const hasLower = /[a-z]/.test(password)
      const hasNumber = /[0-9]/.test(password)
      const hasSpecial = /[^A-Za-z0-9]/.test(password) // Acepta cualquier carácter especial (@, !, #, $, %, etc.)
  
      const isValid = minLength && hasUpper && hasLower && hasNumber && hasSpecial
  
      return {
        minLength,
        hasUpper,
        hasLower,
        hasNumber,
        hasSpecial,
        isValid
      }
    }
  }