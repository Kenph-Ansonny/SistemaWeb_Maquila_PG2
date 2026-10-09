export const allowOnly = {
  // Solo letras minúsculas/mayúsculas y números (sin espacios)
  usuarioInput(e) {
    const char = String.fromCharCode(e.keyCode || e.which)
    if (!/^[a-zA-Z0-9_]$/.test(char)) {
      e.preventDefault()
    }
  },

  // Códigos de catálogo (TEL-001, IN-04): Mayúsculas, números y guiones
  codigoInput(e) {
    const char = String.fromCharCode(e.keyCode || e.which)
    if (!/^[a-zA-Z0-9_-]$/.test(char)) {
      e.preventDefault()
    }
  },

  // Nombres de almacenes, artículos, roles y clientes
  nombreInput(e) {
    const char = String.fromCharCode(e.keyCode || e.which)
    if (!/^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.\/]$/.test(char)) {
      e.preventDefault()
    }
  },

  // Teléfonos (números, +, guión, espacio y paréntesis)
  telefonoInput(e) {
    const char = String.fromCharCode(e.keyCode || e.which)
    if (!/^[0-9\+\-\s\(\)]$/.test(char)) {
      e.preventDefault()
    }
  },

  // Descripciones, observaciones y direcciones generales
  descripcionInput(e) {
    const char = String.fromCharCode(e.keyCode || e.which)
    if (!/^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.\,\:\(\)\/]$/.test(char)) {
      e.preventDefault()
    }
  }
}

export const TextRules = {
  esCodigoValido(val) {
    if (!val) return false
    return /^[A-Z0-9_-]{2,30}$/.test(val.trim().toUpperCase())
  },
  esNombreValido(val, min = 3, max = 150) {
    if (!val) return false
    const regex = new RegExp(`^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\\s\\-\\.\\/]{${min},${max}}$`)
    return regex.test(val.trim())
  },
  esTelefonoValido(val) {
    if (!val) return true // Opcional
    return /^[0-9\+\-\s\(\)]{7,20}$/.test(val.trim())
  },
  esDescripcionValida(val, max = 250) {
    if (!val) return true // Opcional
    const regex = new RegExp(`^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\\s\\-\\.\\,\\:\\(\\)\\/]{0,${max}}$`)
    return regex.test(val.trim())
  }
}

export const PasswordRules = {
  validate(password) {
    const p = password || ''
    return {
      minLength: p.length >= 8,
      hasUpper: /[A-Z]/.test(p),
      hasLower: /[a-z]/.test(p),
      hasNumber: /[0-9]/.test(p),
      hasSpecial: /[@$!%*#?&]/.test(p),
      get isValid() {
        return this.minLength && this.hasUpper && this.hasLower && this.hasNumber && this.hasSpecial
      }
    }
  }
}

export const EmailRules = {
  validate(email) {
    const e = (email || '').trim()
    const pattern = /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/
    return {
      hasAt: e.includes('@'),
      hasDomain: /\.[a-zA-Z]{2,}$/.test(e),
      noSpaces: !/\s/.test(e),
      isValid: pattern.test(e)
    }
  }
}