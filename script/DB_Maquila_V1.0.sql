CREATE DATABASE IF NOT EXISTS MaquilaDb CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE MaquilaDb;

-- =====================================================
-- SEGURIDAD, AUTENTICACIÓN Y AUDITORÍA
-- =====================================================
CREATE TABLE Usuarios (
    id_usuario INT AUTO_INCREMENT PRIMARY KEY,
    nombre_usuario VARCHAR(150) NOT NULL,
    correo VARCHAR(100) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    estado_usuario TINYINT(1) NOT NULL DEFAULT 1,
    fecha_creacion DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    intentos_fallidos TINYINT NOT NULL DEFAULT 0,
    fecha_bloqueo DATETIME NULL,
    fecha_ultimo_acceso DATETIME NULL
) ENGINE=InnoDB;

CREATE TABLE Password_Reset_Tokens (
    id_token INT AUTO_INCREMENT PRIMARY KEY,
    id_usuario INT NOT NULL,
    token VARCHAR(255) NOT NULL UNIQUE,
    fecha_expiracion DATETIME NOT NULL,
    usado TINYINT(1) NOT NULL DEFAULT 0,
    FOREIGN KEY (id_usuario) REFERENCES Usuarios(id_usuario)
) ENGINE=InnoDB;

CREATE TABLE Roles (
    id_rol INT AUTO_INCREMENT PRIMARY KEY,
    nombre_rol VARCHAR(50) NOT NULL UNIQUE,
    descripcion VARCHAR(200) NULL,
    estado_rol TINYINT(1) NOT NULL DEFAULT 1
) ENGINE=InnoDB;

CREATE TABLE Modulos (
    id_modulo INT AUTO_INCREMENT PRIMARY KEY,
    codigo_modulo VARCHAR(50) NOT NULL UNIQUE,
    nombre_modulo VARCHAR(100) NOT NULL,
    estado_modulo TINYINT(1) NOT NULL DEFAULT 1
) ENGINE=InnoDB;

CREATE TABLE Usuario_Roles (
    id_usuario INT NOT NULL,
    id_rol INT NOT NULL,
    PRIMARY KEY (id_usuario, id_rol),
    FOREIGN KEY (id_usuario) REFERENCES Usuarios(id_usuario) ON DELETE CASCADE,
    FOREIGN KEY (id_rol) REFERENCES Roles(id_rol) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE Permisos_Rol (
    id_rol INT NOT NULL,
    id_modulo INT NOT NULL,
    puede_consultar TINYINT(1) NOT NULL DEFAULT 0,
    puede_insertar TINYINT(1) NOT NULL DEFAULT 0,
    puede_modificar TINYINT(1) NOT NULL DEFAULT 0,
    puede_eliminar TINYINT(1) NOT NULL DEFAULT 0,
    PRIMARY KEY (id_rol, id_modulo),
    FOREIGN KEY (id_rol) REFERENCES Roles(id_rol) ON DELETE CASCADE,
    FOREIGN KEY (id_modulo) REFERENCES Modulos(id_modulo) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE Bitacora (
    id_bitacora BIGINT AUTO_INCREMENT PRIMARY KEY,
    id_usuario INT NOT NULL,
    id_modulo INT NOT NULL,
    accion VARCHAR(20) NOT NULL,
    tabla_afectada VARCHAR(50) NOT NULL,
    id_registro VARCHAR(50) NOT NULL,
    valores_anteriores JSON NULL,
    valores_nuevos JSON NULL,
    direccion_ip VARCHAR(45) NULL,
    fecha_registro DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (id_usuario) REFERENCES Usuarios(id_usuario),
    FOREIGN KEY (id_modulo) REFERENCES Modulos(id_modulo)
) ENGINE=InnoDB;

-- =====================================================
-- CATÁLOGOS BASE E INVENTARIO
-- =====================================================
CREATE TABLE Unidades_Medida (
    id_unidad_medida INT AUTO_INCREMENT PRIMARY KEY,
    codigo_medida VARCHAR(10) NOT NULL UNIQUE,
    nombre_medida VARCHAR(50) NOT NULL,
    tipo_medida ENUM('Longitud', 'Masa', 'Unidad', 'Volumen') NOT NULL
) ENGINE=InnoDB;

CREATE TABLE Unidad_Conversiones (
    id_unidad_origen INT NOT NULL,
    id_unidad_destino INT NOT NULL,
    factor_conversion DECIMAL(12, 6) NOT NULL,
    PRIMARY KEY (id_unidad_origen, id_unidad_destino),
    FOREIGN KEY (id_unidad_origen) REFERENCES Unidades_Medida(id_unidad_medida),
    FOREIGN KEY (id_unidad_destino) REFERENCES Unidades_Medida(id_unidad_medida)
) ENGINE=InnoDB;

/*
	-- Pendiente de revisar 
CREATE TABLE Articulo_Factor_Conversion (
    id_articulo INT NOT NULL,
    id_unidad_compra INT NOT NULL,   -- ej. Kilogramo
    id_unidad_consumo INT NOT NULL,  -- ej. Metro
    factor_conversion DECIMAL(12,6) NOT NULL, -- específico de la tela
    PRIMARY KEY (id_articulo, id_unidad_compra, id_unidad_consumo),
    FOREIGN KEY (id_articulo) REFERENCES Articulos(id_articulo),
    FOREIGN KEY (id_unidad_compra) REFERENCES Unidades_Medida(id_unidad_medida),
    FOREIGN KEY (id_unidad_consumo) REFERENCES Unidades_Medida(id_unidad_medida)
) ENGINE=InnoDB;
*/

CREATE TABLE Almacenes (
    id_almacen INT AUTO_INCREMENT PRIMARY KEY,
    nombre_almacen VARCHAR(80) NOT NULL,
    estado_almacen TINYINT(1) NOT NULL DEFAULT 1
) ENGINE=InnoDB;

CREATE TABLE Articulos (
    id_articulo INT AUTO_INCREMENT PRIMARY KEY,
    codigo_articulo VARCHAR(50) NOT NULL UNIQUE,
    nombre_articulo VARCHAR(150) NOT NULL,
    tipo_articulo ENUM('MateriaPrima', 'Insumo', 'PrendaTerminada', 'PiezaCorte') NOT NULL,
    id_unidad_base_medida INT NOT NULL,
    stock_minimo DECIMAL(12, 2) NOT NULL DEFAULT 0.00,
    costo_promedio DECIMAL(12, 4) NOT NULL DEFAULT 0.0000,
    estado_articulo TINYINT(1) NOT NULL DEFAULT 1,
    FOREIGN KEY (id_unidad_base_medida) REFERENCES Unidades_Medida(id_unidad_medida)
) ENGINE=InnoDB;

CREATE TABLE Existencias (
    id_articulo INT NOT NULL,
    id_almacen INT NOT NULL,
    stock_actual DECIMAL(12,4) NOT NULL DEFAULT 0.0000,
    PRIMARY KEY (id_articulo, id_almacen),
    FOREIGN KEY (id_articulo) REFERENCES Articulos(id_articulo),
    FOREIGN KEY (id_almacen) REFERENCES Almacenes(id_almacen)
) ENGINE=InnoDB;

-- Talla y color como un extra, pueden agregarse mas variantes
CREATE TABLE Producto_Terminado_Detalle (
    id_articulo INT PRIMARY KEY,
    talla VARCHAR(10) NULL, 
    color VARCHAR(30) NULL,
    FOREIGN KEY (id_articulo) REFERENCES Articulos(id_articulo)
) ENGINE=InnoDB;

-- =====================================================
-- RECETAS / FICHAS TÉCNICAS (BOM)
-- =====================================================
CREATE TABLE Recetas (
    id_receta INT AUTO_INCREMENT PRIMARY KEY,
    id_articulo_prenda INT NOT NULL,
    nombre_receta VARCHAR(100) NOT NULL,
    descripcion_receta TEXT NULL,
    estado_receta TINYINT(1) NOT NULL DEFAULT 1,
    FOREIGN KEY (id_articulo_prenda) REFERENCES Articulos(id_articulo)
) ENGINE=InnoDB;

CREATE TABLE Receta_Detalles (
    id_receta INT NOT NULL,
    id_articulo_insumo INT NOT NULL,
    id_unidad_consumo INT NOT NULL,
    cantidad_neta DECIMAL(12, 4) NOT NULL, -- Cantidad teórica exacta en prenda
    porcentaje_merma DECIMAL(5, 2) NOT NULL DEFAULT 0.00, -- % Retazo/desperdicio estimado en corte
    cantidad_bruta DECIMAL(12, 4) NOT NULL, -- Cantidad real requerida: Neta * (1 + (Merma/100))
    PRIMARY KEY (id_receta, id_articulo_insumo),
    FOREIGN KEY (id_receta) REFERENCES Recetas(id_receta) ON DELETE CASCADE,
    FOREIGN KEY (id_articulo_insumo) REFERENCES Articulos(id_articulo),
    FOREIGN KEY (id_unidad_consumo) REFERENCES Unidades_Medida(id_unidad_medida)
) ENGINE=InnoDB;

-- =====================================================
-- MÓDULO DE COMPRAS
-- =====================================================
CREATE TABLE Proveedores (
    id_proveedor INT AUTO_INCREMENT PRIMARY KEY,
    nombre_proveedor VARCHAR(150) NOT NULL,
    contacto_principal VARCHAR(100) NULL,
    telefono VARCHAR(30) NULL,
    direccion VARCHAR(200) NULL,
    estado_proveedor TINYINT(1) NOT NULL DEFAULT 1
) ENGINE=InnoDB;

CREATE TABLE Compras (
    id_compra INT AUTO_INCREMENT PRIMARY KEY,
    numero_documento VARCHAR(50) NOT NULL,
    id_proveedor INT NOT NULL,
    id_usuario INT NOT NULL,
    fecha_compra DATETIME NOT NULL,
    estado_compra ENUM('Pendiente', 'Recibido', 'Anulado') NOT NULL DEFAULT 'Pendiente',
    costo_total DECIMAL(14, 2) NOT NULL DEFAULT 0.00,
    UNIQUE KEY uq_compra_documento (id_proveedor, numero_documento),
    FOREIGN KEY (id_proveedor) REFERENCES Proveedores(id_proveedor),
    FOREIGN KEY (id_usuario) REFERENCES Usuarios(id_usuario)
) ENGINE=InnoDB;

CREATE TABLE CompraDetalles (
    id_detalle BIGINT AUTO_INCREMENT PRIMARY KEY,
    id_compra INT NOT NULL,
    id_articulo INT NOT NULL,
    id_unidad_medida INT NOT NULL,
    cantidad DECIMAL(12, 2) NOT NULL,
    costo_unitario DECIMAL(12, 4) NOT NULL,
    subtotal DECIMAL(14, 2) NOT NULL,
    id_almacen_destino INT NOT NULL,
    FOREIGN KEY (id_compra) REFERENCES Compras(id_compra) ON DELETE CASCADE,
    FOREIGN KEY (id_articulo) REFERENCES Articulos(id_articulo),
    FOREIGN KEY (id_unidad_medida) REFERENCES Unidades_Medida(id_unidad_medida),
    FOREIGN KEY (id_almacen_destino) REFERENCES Almacenes(id_almacen)
) ENGINE=InnoDB;

-- =====================================================
-- MÓDULO DE PEDIDOS, PRODUCCIÓN Y MERMAS
-- =====================================================
CREATE TABLE Clientes (
    id_cliente INT AUTO_INCREMENT PRIMARY KEY,
    nombre_cliente VARCHAR(150) NOT NULL,
    telefono_cliente VARCHAR(30) NULL,
    direccion_cliente VARCHAR(200) NULL,
    estado_cliente TINYINT(1) NOT NULL DEFAULT 1
) ENGINE=InnoDB;

CREATE TABLE Pedidos (
    id_pedido INT AUTO_INCREMENT PRIMARY KEY,
    numero_pedido VARCHAR(30) NOT NULL UNIQUE,
    id_cliente INT NOT NULL,
    id_usuario INT NOT NULL,
    fecha_emision DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    fecha_estimada_entrega DATE NULL,
    estado_pedido ENUM('Registrado', 'En Produccion', 'Finalizado', 'Entregado', 'Cancelado') NOT NULL DEFAULT 'Registrado',
    FOREIGN KEY (id_cliente) REFERENCES Clientes(id_cliente),
    FOREIGN KEY (id_usuario) REFERENCES Usuarios(id_usuario)
) ENGINE=InnoDB;

CREATE TABLE Pedido_Detalles (
    id_detalle BIGINT AUTO_INCREMENT PRIMARY KEY,
    id_pedido INT NOT NULL,
    id_articulo_prenda INT NOT NULL,
    id_receta INT NOT NULL,
    cantidad INT NOT NULL,
    precio_unitario_acordado DECIMAL(12, 2) NOT NULL,
    FOREIGN KEY (id_pedido) REFERENCES Pedidos(id_pedido) ON DELETE CASCADE,
    FOREIGN KEY (id_articulo_prenda) REFERENCES Articulos(id_articulo),
    FOREIGN KEY (id_receta) REFERENCES Recetas(id_receta)
) ENGINE=InnoDB;

CREATE TABLE Ordenes_Produccion (
    id_orden INT AUTO_INCREMENT PRIMARY KEY,
    id_pedido_detalle BIGINT NOT NULL,
    fecha_inicio DATETIME NOT NULL,
    fecha_fin DATETIME NULL,
    cantidad_programada INT NOT NULL,
    cantidad_producida INT NOT NULL DEFAULT 0,
    estado_orden ENUM('Iniciada', 'En Corte', 'En Costura', 'Terminada') NOT NULL DEFAULT 'Iniciada',
    FOREIGN KEY (id_pedido_detalle) REFERENCES Pedido_Detalles(id_detalle)
) ENGINE=InnoDB;

CREATE TABLE Registro_Mermas (
    id_merma BIGINT AUTO_INCREMENT PRIMARY KEY,
    id_orden_produccion INT NOT NULL,
    id_articulo_insumo INT NOT NULL,
    cantidad_merma_real DECIMAL(12, 4) NOT NULL,
    tipo_merma ENUM('Retazo Aprovechable', 'Desperdicio Irrecuperable', 'Prenda Defectuosa') NOT NULL,
    id_almacen_destino INT NULL,
    observaciones TEXT NULL,
    fecha_registro DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (id_orden_produccion) REFERENCES Ordenes_Produccion(id_orden),
    FOREIGN KEY (id_articulo_insumo) REFERENCES Articulos(id_articulo),
    FOREIGN KEY (id_almacen_destino) REFERENCES Almacenes(id_almacen)
) ENGINE=InnoDB;

-- =====================================================
-- KARDEX TRANSACCIONAL GENERAL
-- =====================================================
CREATE TABLE Kardex_Movimientos (
    id_kardex BIGINT AUTO_INCREMENT PRIMARY KEY,
    id_articulo INT NOT NULL,
    id_almacen INT NOT NULL,
    tipo_movimiento ENUM('ENTRADA_COMPRA', 'CONSUMO_PRODUCCION', 'ENTRADA_PRODUCIDO', 'MERMA_CORTE', 'SALIDA_PEDIDO', 'AJUSTE_INVENTARIO') NOT NULL,
    tipo_documento_referencia VARCHAR(50) NOT NULL,
    id_documento_referencia BIGINT NOT NULL,
    cantidad DECIMAL(12, 4) NOT NULL,
    stock_anterior DECIMAL(12, 4) NOT NULL,
    stock_posterior DECIMAL(12, 4) NOT NULL,
    costo_unitario DECIMAL(12, 4) NOT NULL,
    id_usuario INT NOT NULL,
    fecha_movimiento DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (id_articulo) REFERENCES Articulos(id_articulo),
    FOREIGN KEY (id_almacen) REFERENCES Almacenes(id_almacen),
    FOREIGN KEY (id_usuario) REFERENCES Usuarios(id_usuario)
) ENGINE=InnoDB;