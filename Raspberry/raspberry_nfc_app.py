"""
Aplicación NFC para Raspberry Pi - QuickTable 2FA
Optimizado para pantalla 800x600
"""

import tkinter as tk
from tkinter import messagebox
import requests
import threading
import time
import json
import os

# Importar librerías del RC522
try:
    from mfrc522 import SimpleMFRC522
    import RPi.GPIO as GPIO
    GPIO.setwarnings(False)
    HARDWARE_AVAILABLE = True
    print("✅ Hardware RC522 detectado")
except ImportError:
    HARDWARE_AVAILABLE = False
    print("❌ Hardware RC522 no disponible - Modo de prueba")

class NumericKeyboard:
    """Teclado numérico en pantalla optimizado para 800x600"""
    def __init__(self, parent, entry_widget):
        self.parent = parent
        self.entry = entry_widget
        self.create_keyboard()
    
    def create_keyboard(self):
        # Frame para el teclado
        self.keyboard_frame = tk.Frame(self.parent, bg='#34495e')
        self.keyboard_frame.pack(pady=15)
        
        # Botones numéricos más compactos
        buttons = [
            ['1', '2', '3'],
            ['4', '5', '6'], 
            ['7', '8', '9'],
            ['⌫', '0', '✓']
        ]
        
        for row_idx, row in enumerate(buttons):
            row_frame = tk.Frame(self.keyboard_frame, bg='#34495e')
            row_frame.pack(pady=3)
            
            for col_idx, btn_text in enumerate(row):
                btn = tk.Button(
                    row_frame,
                    text=btn_text,
                    font=('Arial', 14, 'bold'),
                    width=4,
                    height=1,
                    bg='#7f8c8d',
                    fg='white',
                    activebackground='#95a5a6',
                    command=lambda x=btn_text: self.on_key_press(x)
                )
                btn.pack(side='left', padx=3)
    
    def on_key_press(self, key):
        current = self.entry.get()
        
        if key == '⌫':  # Backspace
            if current:
                self.entry.delete(len(current)-1, tk.END)
        elif key == '✓':  # Enter
            if len(current) == 6:
                self.parent.master.verify_session()
        elif key.isdigit() and len(current) < 6:
            self.entry.insert(tk.END, key)

class NFCApp:
    def __init__(self):
        self.server_url = ""
        self.session_code = ""
        self.session_data = {}
        self.root = None
        
        # Inicializar hardware RC522
        self.reader = None
        if HARDWARE_AVAILABLE:
            try:
                self.reader = SimpleMFRC522()
                print("✅ RC522 inicializado correctamente")
            except Exception as e:
                print(f"❌ Error inicializando RC522: {e}")
                self.reader = None
    
    def setup_window(self):
        """Configuración básica de ventana para 800x600"""
        self.root = tk.Tk()
        self.root.title("QuickTable 2FA - Raspberry Pi")
        self.root.geometry("800x600")
        self.root.resizable(False, False)  # Fijo para pantalla específica
        self.root.configure(bg='#2c3e50')
        
        # Centrar ventana
        self.root.eval('tk::PlaceWindow . center')
        
        # ESC para salir
        self.root.bind('<Escape>', lambda e: self.root.quit())
    
    def show_server_config(self):
        """Pantalla inicial para configurar servidor"""
        self.setup_window()
        
        # Limpiar ventana
        for widget in self.root.winfo_children():
            widget.destroy()
        
        # Frame principal
        main_frame = tk.Frame(self.root, bg='#2c3e50')
        main_frame.place(relx=0.5, rely=0.5, anchor='center')
        
        # Título
        title_label = tk.Label(
            main_frame,
            text="QuickTable 2FA",
            font=('Arial', 24, 'bold'),
            fg='white',
            bg='#2c3e50'
        )
        title_label.pack(pady=(0, 20))
        
        # Subtítulo
        subtitle_label = tk.Label(
            main_frame,
            text="Configuración del Servidor",
            font=('Arial', 16),
            fg='#bdc3c7',
            bg='#2c3e50'
        )
        subtitle_label.pack(pady=(0, 30))
        
        # Estado del hardware
        if HARDWARE_AVAILABLE and self.reader:
            hw_status = "✅ Hardware RC522: Conectado"
            hw_color = '#27ae60'
        else:
            hw_status = "❌ Hardware RC522: No disponible"
            hw_color = '#e74c3c'
        
        hw_label = tk.Label(
            main_frame,
            text=hw_status,
            font=('Arial', 11),
            fg=hw_color,
            bg='#2c3e50'
        )
        hw_label.pack(pady=(0, 20))
        
        # Campo URL del servidor
        tk.Label(
            main_frame, 
            text="Dirección IP del Servidor:",
            font=('Arial', 12, 'bold'),
            fg='white',
            bg='#2c3e50'
        ).pack(pady=(0, 5))
        
        self.server_ip_entry = tk.Entry(
            main_frame,
            font=('Arial', 14),
            width=20,
            justify='center'
        )
        self.server_ip_entry.pack(pady=10)
        self.server_ip_entry.insert(0, "192.168.1.100")  # Valor por defecto
        self.server_ip_entry.focus()
        
        # Campo puerto
        tk.Label(
            main_frame, 
            text="Puerto (opcional, por defecto 5000):",
            font=('Arial', 12, 'bold'),
            fg='white',
            bg='#2c3e50'
        ).pack(pady=(20, 5))
        
        self.server_port_entry = tk.Entry(
            main_frame,
            font=('Arial', 14),
            width=10,
            justify='center'
        )
        self.server_port_entry.pack(pady=10)
        self.server_port_entry.insert(0, "5000")
        
        # Status de conexión
        self.connection_status = tk.Label(
            main_frame,
            text="Ingrese la dirección IP del servidor QuickTable",
            font=('Arial', 11),
            fg='#95a5a6',
            bg='#2c3e50',
            wraplength=400
        )
        self.connection_status.pack(pady=15)
        
        # Botones
        button_frame = tk.Frame(main_frame, bg='#2c3e50')
        button_frame.pack(pady=20)
        
        test_button = tk.Button(
            button_frame,
            text="Probar Conexión",
            font=('Arial', 12, 'bold'),
            bg='#f39c12',
            fg='white',
            width=12,
            height=2,
            command=self.test_server_connection
        )
        test_button.pack(side='left', padx=10)
        
        connect_button = tk.Button(
            button_frame,
            text="Conectar",
            font=('Arial', 12, 'bold'),
            bg='#27ae60',
            fg='white',
            width=12,
            height=2,
            command=self.save_server_and_continue
        )
        connect_button.pack(side='left', padx=10)
        
        # Bind Enter key
        self.server_ip_entry.bind('<Return>', lambda e: self.test_server_connection())
        self.server_port_entry.bind('<Return>', lambda e: self.save_server_and_continue())
        
    def test_server_connection(self):
        """Probar conexión al servidor"""
        ip = self.server_ip_entry.get().strip()
        port = self.server_port_entry.get().strip() or "5000"
        
        if not ip:
            self.connection_status.config(text="❌ Ingrese una dirección IP", fg='#e74c3c')
            return
        
        test_url = f"http://{ip}:{port}"
        self.connection_status.config(text="🔄 Probando conexión...", fg='#f39c12')
        
        # Probar en hilo separado
        threading.Thread(target=self._test_connection_thread, args=(test_url,), daemon=True).start()
    
    def _test_connection_thread(self, test_url):
        """Hilo para probar conexión"""
        try:
            response = requests.get(f"{test_url}/api/tarjeta/pendientes", timeout=5)
            
            if response.status_code in [200, 401]:
                self.root.after(0, lambda: self.connection_status.config(
                    text="✅ Conexión exitosa - El servidor responde correctamente", 
                    fg='#27ae60'
                ))
                self.server_url = test_url
                
            else:
                self.root.after(0, lambda: self.connection_status.config(
                    text=f"❌ Servidor responde pero con error ({response.status_code})", 
                    fg='#e74c3c'
                ))
                
        except requests.exceptions.ConnectTimeout:
            self.root.after(0, lambda: self.connection_status.config(
                text="❌ Tiempo de espera agotado - Verifique la IP", 
                fg='#e74c3c'
            ))
        except requests.exceptions.ConnectionError:
            self.root.after(0, lambda: self.connection_status.config(
                text="❌ No se puede conectar - Verifique IP y puerto", 
                fg='#e74c3c'
            ))
        except Exception as e:
            self.root.after(0, lambda: self.connection_status.config(
                text=f"❌ Error: {str(e)[:50]}...", 
                fg='#e74c3c'
            ))
    
    def save_server_and_continue(self):
        """Guardar servidor y continuar a pantalla principal"""
        ip = self.server_ip_entry.get().strip()
        port = self.server_port_entry.get().strip() or "5000"
        
        if not ip:
            self.connection_status.config(text="❌ Ingrese una dirección IP", fg='#e74c3c')
            return
        
        self.server_url = f"http://{ip}:{port}"
        print(f"🌐 Servidor configurado: {self.server_url}")
        
        # Guardar en archivo para próximas sesiones
        try:
            config = {"server_url": self.server_url}
            with open('config.json', 'w') as f:
                json.dump(config, f, indent=4)
        except:
            pass  # No es crítico si no se puede guardar
        
        self.show_main_screen()
    
    def show_main_screen(self):
        """Pantalla principal después de configurar servidor"""
        # Limpiar ventana
        for widget in self.root.winfo_children():
            widget.destroy()
        
        # Frame principal
        main_frame = tk.Frame(self.root, bg='#2c3e50')
        main_frame.place(relx=0.5, rely=0.5, anchor='center')
        
        # Título
        title_label = tk.Label(
            main_frame, 
            text="QuickTable 2FA",
            font=('Arial', 24, 'bold'),
            fg='white',
            bg='#2c3e50'
        )
        title_label.pack(pady=(0, 20))
        
        # Información del servidor
        server_label = tk.Label(
            main_frame,
            text=f"📡 Servidor: {self.server_url}",
            font=('Arial', 10),
            fg='#3498db',
            bg='#2c3e50'
        )
        server_label.pack(pady=(0, 30))
        
        # Botón iniciar
        self.start_button = tk.Button(
            main_frame,
            text="INICIAR",
            font=('Arial', 18, 'bold'),
            bg='#3498db',
            fg='white',
            width=15,
            height=3,
            command=self.show_session_input
        )
        self.start_button.pack(pady=20)
        
        # Status
        status_label = tk.Label(
            main_frame,
            text="Presione INICIAR para comenzar",
            font=('Arial', 12),
            fg='#bdc3c7',
            bg='#2c3e50'
        )
        status_label.pack(pady=15)
        
        # Botón configurar servidor
        config_button = tk.Button(
            main_frame,
            text="⚙️ Cambiar Servidor",
            font=('Arial', 10),
            bg='#95a5a6',
            fg='white',
            width=15,
            height=1,
            command=self.show_server_config
        )
        config_button.pack(pady=(30, 0))
    
    def show_session_input(self):
        """Pantalla para ingresar código de sesión"""
        # Limpiar ventana
        for widget in self.root.winfo_children():
            widget.destroy()
        
        # Frame principal
        main_frame = tk.Frame(self.root, bg='#2c3e50')
        main_frame.place(relx=0.5, rely=0.5, anchor='center')
        
        # Título más compacto
        title_label = tk.Label(
            main_frame, 
            text="Código de Sesión",
            font=('Arial', 20, 'bold'),
            fg='white',
            bg='#2c3e50'
        )
        title_label.pack(pady=(0, 20))
        
        # Instrucciones
        instruction_label = tk.Label(
            main_frame,
            text="Ingrese el código de 6 dígitos\nque aparece en la pantalla del PC",
            font=('Arial', 12),
            fg='#ecf0f1',
            bg='#2c3e50',
            justify='center'
        )
        instruction_label.pack(pady=10)
        
        # Entry para código
        self.code_entry = tk.Entry(
            main_frame,
            font=('Arial', 20, 'bold'),
            width=8,
            justify='center',
            validate='key',
            bg='white',
            fg='#2c3e50'
        )
        # Validar solo números
        vcmd = (self.root.register(self.validate_number), '%P')
        self.code_entry.config(validate='key', validatecommand=vcmd)
        self.code_entry.pack(pady=15)
        
        # Teclado numérico compacto
        self.keyboard = NumericKeyboard(main_frame, self.code_entry)
        
        # Botones de acción
        button_frame = tk.Frame(main_frame, bg='#2c3e50')
        button_frame.pack(pady=15)
        
        back_button = tk.Button(
            button_frame,
            text="Atrás",
            font=('Arial', 12),
            bg='#95a5a6',
            fg='white',
            width=10,
            height=2,
            command=self.show_main_screen
        )
        back_button.pack(side='left', padx=10)
        
        self.verify_button = tk.Button(
            button_frame,
            text="Verificar",
            font=('Arial', 12, 'bold'),
            bg='#27ae60',
            fg='white',
            width=10,
            height=2,
            command=self.verify_session,
            state='disabled'
        )
        self.verify_button.pack(side='left', padx=10)
        
    def validate_number(self, value):
        """Validar entrada solo números hasta 6 dígitos"""
        if len(value) <= 6 and (value.isdigit() or value == ""):
            # Habilitar botón verificar solo con 6 dígitos
            if hasattr(self, 'verify_button'):
                if len(value) == 6:
                    self.verify_button.config(state='normal', bg='#27ae60')
                else:
                    self.verify_button.config(state='disabled', bg='#7f8c8d')
            return True
        return False
    
    def verify_session(self):
        """Verificar código de sesión con el servidor"""
        code = self.code_entry.get().strip()
        
        if len(code) != 6:
            messagebox.showerror("Error", "Debe ingresar un código de 6 dígitos")
            return
            
        self.session_code = code
        
        # Mostrar loading en el botón
        self.verify_button.config(text="Verificando...", state='disabled', bg='#f39c12')
        
        # Verificar en hilo separado
        threading.Thread(target=self.check_session_thread, daemon=True).start()
        
    def check_session_thread(self):
        """Verificar sesión en hilo separado"""
        try:
            response = requests.post(
                f"{self.server_url}/api/tarjeta/validar-sesion",
                data={'sessionCode': self.session_code},
                timeout=10
            )
            
            if response.status_code == 200:
                data = response.json()
                if data.get('valid'):
                    self.session_data = data
                    self.root.after(0, self.on_session_valid)
                else:
                    self.root.after(0, lambda: self.show_error("Código de sesión inválido"))
            else:
                self.root.after(0, lambda: self.show_error("Error de conexión con el servidor"))
                
        except requests.exceptions.RequestException as e:
            error_msg = f"Error de conexión: {str(e)}"
            print(f"DEBUG: {error_msg}")
            self.root.after(0, lambda: self.show_error(error_msg))
    
    def show_error(self, message):
        """Mostrar error y restaurar botón"""
        if hasattr(self, 'verify_button'):
            self.verify_button.config(text="Verificar", state='normal', bg='#27ae60')
        
        messagebox.showerror("Error", message)
    
    def on_session_valid(self):
        """Sesión válida, determinar flujo según rol"""
        role = self.session_data.get('role', 'unknown')
        
        if role == 'TI':
            self.show_nfc_write_screen()
        elif role == 'Admin':
            self.show_nfc_read_screen()
        else:
            messagebox.showerror("Error", f"Rol no reconocido: {role}")
    
    def show_nfc_write_screen(self):
        """Pantalla para GRABAR tarjeta (modo TI)"""
        # Limpiar ventana
        for widget in self.root.winfo_children():
            widget.destroy()
        
        # Frame principal
        main_frame = tk.Frame(self.root, bg='#2c3e50')
        main_frame.place(relx=0.5, rely=0.5, anchor='center')
        
        # Título
        title_label = tk.Label(
            main_frame, 
            text="Grabar Tarjeta NFC",
            font=('Arial', 20, 'bold'),
            fg='white',
            bg='#2c3e50'
        )
        title_label.pack(pady=(0, 20))
        
        # Info compacta
        info_frame = tk.Frame(main_frame, bg='#2c3e50')
        info_frame.pack(pady=10)
        
        tk.Label(info_frame, text=f"Sesión TI - Código: {self.session_code}",
                font=('Arial', 11), fg='#3498db', bg='#2c3e50').pack()
        tk.Label(info_frame, text=f"UID: {self.session_data.get('uid', 'N/A')}",
                font=('Arial', 11, 'bold'), fg='#e74c3c', bg='#2c3e50').pack()
        
        # Instrucciones
        self.instruction_label = tk.Label(
            main_frame,
            text="Acerque la tarjeta al lector para GRABAR...",
            font=('Arial', 14),
            fg='#ecf0f1',
            bg='#2c3e50'
        )
        self.instruction_label.pack(pady=20)
        
        # Status
        self.nfc_status_label = tk.Label(
            main_frame,
            text="⏳ Esperando tarjeta...",
            font=('Arial', 12),
            fg='#f39c12',
            bg='#2c3e50'
        )
        self.nfc_status_label.pack(pady=15)
        
        # Botón volver
        back_button = tk.Button(
            main_frame,
            text="Volver al Inicio",
            font=('Arial', 11),
            bg='#95a5a6',
            fg='white',
            width=15,
            height=2,
            command=self.show_main_screen
        )
        back_button.pack(pady=20)
        
        # Iniciar proceso NFC automático
        threading.Thread(target=self.nfc_write_process, daemon=True).start()
    
    def show_nfc_read_screen(self):
        """Pantalla para LEER tarjeta (modo Admin)"""
        # Limpiar ventana
        for widget in self.root.winfo_children():
            widget.destroy()
        
        # Frame principal
        main_frame = tk.Frame(self.root, bg='#2c3e50')
        main_frame.place(relx=0.5, rely=0.5, anchor='center')
        
        # Título
        title_label = tk.Label(
            main_frame, 
            text="Autenticación Admin",
            font=('Arial', 20, 'bold'),
            fg='white',
            bg='#2c3e50'
        )
        title_label.pack(pady=(0, 20))
        
        # Info
        info_label = tk.Label(
            main_frame,
            text=f"Sesión Admin - Código: {self.session_code}",
            font=('Arial', 11),
            fg='#3498db',
            bg='#2c3e50'
        )
        info_label.pack(pady=10)
        
        # Instrucciones
        self.instruction_label = tk.Label(
            main_frame,
            text="Acerque su tarjeta al lector para AUTENTICAR...",
            font=('Arial', 14),
            fg='#ecf0f1',
            bg='#2c3e50'
        )
        self.instruction_label.pack(pady=20)
        
        # Status
        self.nfc_status_label = tk.Label(
            main_frame,
            text="⏳ Esperando tarjeta...",
            font=('Arial', 12),
            fg='#f39c12',
            bg='#2c3e50'
        )
        self.nfc_status_label.pack(pady=15)
        
        # Botón volver
        back_button = tk.Button(
            main_frame,
            text="Volver al Inicio",
            font=('Arial', 11),
            bg='#95a5a6',
            fg='white',
            width=15,
            height=2,
            command=self.show_main_screen
        )
        back_button.pack(pady=20)
        
        # Iniciar proceso NFC automático
        threading.Thread(target=self.nfc_read_process, daemon=True).start()
    
    def nfc_write_process(self):
        """Proceso real de escritura NFC"""
        if not HARDWARE_AVAILABLE or not self.reader:
            self.root.after(0, lambda: self.show_error("Hardware RC522 no disponible"))
            return
        
        uid_to_write = self.session_data.get('uid', 'UNKNOWN')
        
        try:
            print(f"Escribiendo UID: {uid_to_write}")
            
            # Escribir tarjeta
            self.reader.write(uid_to_write)
            
            self.root.after(0, lambda: self.nfc_status_label.config(
                text="✅ Grabada! Retire y recoloque para verificar...", fg='#27ae60'))
            
            time.sleep(3)
            
            # Verificar lectura
            self.root.after(0, lambda: self.nfc_status_label.config(
                text="🔍 Verificando...", fg='#f39c12'))
            
            read_id, read_text = self.reader.read()
            
            if read_text.strip() == uid_to_write:
                self.root.after(0, lambda: self.on_write_verify_success(uid_to_write))
            else:
                self.root.after(0, lambda: self.show_error(
                    f"Error verificación. Leído: {read_text.strip()}"))
                
        except Exception as e:
            self.root.after(0, lambda: self.show_error(f"Error: {str(e)}"))
        finally:
            if HARDWARE_AVAILABLE:
                GPIO.cleanup()
    
    def nfc_read_process(self):
        """Proceso real de lectura NFC para Admin"""
        if not HARDWARE_AVAILABLE or not self.reader:
            self.root.after(0, lambda: self.show_error("Hardware RC522 no disponible"))
            return
        
        try:
            print("Esperando tarjeta de Admin...")
            
            # Leer tarjeta
            read_id, read_uid = self.reader.read()
            
            self.root.after(0, lambda: self.nfc_status_label.config(
                text="✅ Tarjeta leída, verificando...", fg='#f39c12'))
            
            # Enviar al servidor
            response = requests.post(
                f"{self.server_url}/Login/Confirmar2FA",
                data={
                    'navId': self.session_data.get('navId', ''),
                    'uid': read_uid.strip()
                },
                timeout=10
            )
            
            if response.status_code == 200:
                self.root.after(0, self.on_auth_success)
            elif response.status_code == 401:
                self.root.after(0, lambda: self.show_error("Tarjeta no autorizada"))
            else:
                self.root.after(0, lambda: self.show_error("Error de autenticación"))
                
        except Exception as e:
            self.root.after(0, lambda: self.show_error(f"Error: {str(e)}"))
        finally:
            if HARDWARE_AVAILABLE:
                GPIO.cleanup()
    
    def on_write_verify_success(self, uid):
        """Confirmar escritura al servidor"""
        try:
            response = requests.post(
                f"{self.server_url}/api/tarjeta/confirmar",
                data={'uidLeido': uid},
                timeout=10
            )
            
            if response.status_code == 200:
                self.nfc_status_label.config(text="🎉 ¡Completado exitosamente!", fg='#27ae60')
                self.instruction_label.config(text="Tarjeta grabada y activada correctamente.")
                self.root.after(5000, self.show_main_screen)
            else:
                self.show_error("Error confirmando con el servidor")
                
        except Exception as e:
            self.show_error(f"Error de red: {str(e)}")
    
    def on_auth_success(self):
        """Autenticación exitosa"""
        self.nfc_status_label.config(text="🎉 ¡Autenticación exitosa!", fg='#27ae60')
        self.instruction_label.config(text="Puede continuar en el navegador.")
        self.root.after(5000, self.show_main_screen)
    
    def load_previous_config(self):
        """Cargar configuración previa si existe"""
        try:
            if os.path.exists('config.json'):
                with open('config.json', 'r') as f:
                    config = json.load(f)
                    return config.get('server_url', '')
        except:
            pass
        return ''
    
    def run(self):
        """Ejecutar aplicación"""
        print("=== QuickTable NFC App - Pantalla 800x600 ===")
        print(f"Hardware RC522: {'✅ Disponible' if HARDWARE_AVAILABLE else '❌ No disponible'}")
        print("=" * 50)
        
        # Cargar configuración previa
        previous_server = self.load_previous_config()
        if previous_server:
            self.server_url = previous_server
            print(f"🔄 Servidor anterior: {previous_server}")
            self.setup_window()
            self.show_main_screen()
        else:
            # Primera vez, pedir configuración
            self.show_server_config()
        
        try:
            self.root.mainloop()
        finally:
            if HARDWARE_AVAILABLE:
                GPIO.cleanup()
                print("GPIO limpiado")

if __name__ == '__main__':
    app = NFCApp()
    app.run()
