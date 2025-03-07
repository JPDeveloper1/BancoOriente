using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;




class Usuario
{
    private static readonly Random random = new Random();

    public string NumeroCuenta { get; set; }
    public string NombreCompleto { get; set; }
    public string NumeroCedula { get; set; }
    private string _contraseña;
    public decimal DineroTotal { get; set; }
    public decimal LimiteDiarioRetiro { get; set; }
    public decimal LimiteDiarioRecarga { get; set; }

    public Usuario(string nombreCompleto, string numeroCedula, string contraseña)
    {
        NumeroCuenta = GenerarNumeroCuenta();
        NombreCompleto = nombreCompleto;
        NumeroCedula = numeroCedula;
        Contraseña = contraseña;
        DineroTotal = 0;
        LimiteDiarioRetiro = 2000000; // Límite inicial para retiros diarios
        LimiteDiarioRecarga = 5000000; // Límite para recargas diarias
    }
}
   

    private string GenerarNumeroCuenta()
    {
        return random.Next(100000, 999999).ToString();
    }

    public string Contraseña
    {
        get { return _contraseña; }
        set
        {
            try
            {
                if (value.Length == 5)
                {
                    _contraseña = value;
                }
                else
                {
                    throw new Exception("La contraseña debe tener exactamente 5 caracteres.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }

    public void MostrarInformacion()
    {
        Console.WriteLine("---------------------");
        Console.WriteLine($"Nombre   | {NombreCompleto}");
        Console.WriteLine($"Cedula   | {NumeroCedula}");
        Console.WriteLine($"Numero de cuenta | {NumeroCuenta}");
        Console.WriteLine($"Dinero total $ | {DineroTotal}");
        Console.WriteLine("---------------------");
    }

    public void Retirar(decimal cantidad)
    {
        using (MySqlConnection connection = new MySqlConnection(connectionString))
        {
            try
            {
                connection.Open();
                string query = "UPDATE Usuario SET dinero_total = dinero_total - @cantidad, limite_diario_retiro = limite_diario_retiro - @cantidad WHERE numero_cuenta = @numeroCuenta";
                MySqlCommand cmd = new MySqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@cantidad", cantidad);
                cmd.Parameters.AddWithValue("@numeroCuenta", this.NumeroCuenta);

                cmd.ExecuteNonQuery();
                Console.Clear();
                Console.WriteLine($"Retiro exitoso. Dinero restante: {DineroTotal - cantidad}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al retirar: " + ex.Message);
            }
        }
    }

    public void Transferir(Usuario destinatario, decimal monto)
    {
        using (MySqlConnection connection = new MySqlConnection(connectionString))
        {
            try
            {
                connection.Open();

                // Iniciar una transacción para asegurar la atomicidad
                MySqlTransaction transaction = connection.BeginTransaction();

                try
                {
                    // Actualizar el saldo del usuario que transfiere
                    string queryRetirar = "UPDATE Usuario SET dinero_total = dinero_total - @monto WHERE numero_cuenta = @numeroCuenta";
                    MySqlCommand cmdRetirar = new MySqlCommand(queryRetirar, connection, transaction);
                    cmdRetirar.Parameters.AddWithValue("@monto", monto);
                    cmdRetirar.Parameters.AddWithValue("@numeroCuenta", this.NumeroCuenta);
                    cmdRetirar.ExecuteNonQuery();

                    // Actualizar el saldo del destinatario
                    string queryDepositar = "UPDATE Usuario SET dinero_total = dinero_total + @monto WHERE numero_cuenta = @numeroCuentaDestinatario";
                    MySqlCommand cmdDepositar = new MySqlCommand(queryDepositar, connection, transaction);
                    cmdDepositar.Parameters.AddWithValue("@monto", monto);
                    cmdDepositar.Parameters.AddWithValue("@numeroCuentaDestinatario", destinatario.NumeroCuenta);
                    cmdDepositar.ExecuteNonQuery();

                    // Registrar la transacción
                    string queryTransaccion = "INSERT INTO Transacciones (usuario_id, tipo_transaccion, monto, destinatario_id) " +
                                              "VALUES ((SELECT id FROM Usuario WHERE numero_cuenta = @numeroCuenta), 'Transferencia', @monto, (SELECT id FROM Usuario WHERE numero_cuenta = @numeroCuentaDestinatario))";
                    MySqlCommand cmdTransaccion = new MySqlCommand(queryTransaccion, connection, transaction);
                    cmdTransaccion.Parameters.AddWithValue("@numeroCuenta", this.NumeroCuenta);
                    cmdTransaccion.Parameters.AddWithValue("@monto", monto);
                    cmdTransaccion.Parameters.AddWithValue("@numeroCuentaDestinatario", destinatario.NumeroCuenta);
                    cmdTransaccion.ExecuteNonQuery();

                    // Confirmar la transacción
                    transaction.Commit();

                    Console.Clear();
                    Console.WriteLine($"Transferencia exitosa a {destinatario.NombreCompleto}. Dinero restante: {DineroTotal - monto}");
                }
                catch (Exception ex)
                {
                    // Revertir la transacción en caso de error
                    transaction.Rollback();
                    Console.WriteLine("Error al transferir: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al conectar a la base de datos: " + ex.Message);
            }
        }
    }

    public void Recargar(decimal monto)
    {
        using (MySqlConnection connection = new MySqlConnection(connectionString))
        {
            try
            {
                connection.Open();

                // Actualizar el saldo del usuario
                string query = "UPDATE Usuario SET dinero_total = dinero_total + @monto, limite_diario_recarga = limite_diario_recarga - @monto WHERE numero_cuenta = @numeroCuenta";
                MySqlCommand cmd = new MySqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@monto", monto);
                cmd.Parameters.AddWithValue("@numeroCuenta", this.NumeroCuenta);
                cmd.ExecuteNonQuery();

                // Registrar la transacción
                string queryTransaccion = "INSERT INTO Transacciones (usuario_id, tipo_transaccion, monto) " +
                                          "VALUES ((SELECT id FROM Usuario WHERE numero_cuenta = @numeroCuenta), 'Recarga', @monto)";
                MySqlCommand cmdTransaccion = new MySqlCommand(queryTransaccion, connection);
                cmdTransaccion.Parameters.AddWithValue("@numeroCuenta", this.NumeroCuenta);
                cmdTransaccion.Parameters.AddWithValue("@monto", monto);
                cmdTransaccion.ExecuteNonQuery();

                Console.Clear();
                Console.WriteLine($"Recarga exitosa. Dinero total: {DineroTotal + monto}");
                Console.WriteLine($"Monto disponible para recargar hoy: {LimiteDiarioRecarga - monto}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al recargar: " + ex.Message);
            }
        }
    }

    class Program
{
   
        // Cadena de conexión a MySQL (XAMPP)
        private static string connectionString = "server=localhost;user=root;database=bancooriente;port=3306;password=";
        static List<Usuario> usuarios = new List<Usuario>();               
    static void Main()
        {

            while (true)
            {
                Console.SetCursorPosition(Console.WindowWidth / 2 - 23, 1);
                Console.WriteLine("-----¡Bienvenido a BANCO ORIENTE!-----");
                Console.SetCursorPosition(0, 5);
                Console.WriteLine("1.Iniciar sesión");
                Console.WriteLine("2. Salir");
                Console.Write("Seleccione una opción: ");
                if (int.TryParse(Console.ReadLine(), out int opcion))
                {
                    switch (opcion)
                    {
                        case 1:
                            IniciarSesion();
                            break;
                        case 2:
                            Console.Clear();
                            Console.WriteLine("Gracias por usar BANCO ORIENTE. ¡Hasta luego!");
                            Environment.Exit(0);
                            break;
                        default:
                            Console.WriteLine("Opción no válida. Por favor, seleccione una opción válida.");
                            break;
                    }
                }
                else
                {
                    Console.Clear();
                    Console.WriteLine("Entrada inválida. Introduce un número.");
                }
            }
        }

    static void IniciarSesion()
    {
        Console.WriteLine("Si no tiene una cuenta, presione Enter para continuar.");
        Console.Write("Ingrese el número de cuenta: ");
        string numeroCuenta = Console.ReadLine();

        using (MySqlConnection connection = new MySqlConnection(connectionString))
        {
            try
            {
                connection.Open();
                string query = "SELECT * FROM Usuario WHERE numero_cuenta = @numeroCuenta";
                MySqlCommand cmd = new MySqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@numeroCuenta", numeroCuenta);

                MySqlDataReader reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    Console.Write("Ingrese la contraseña: ");
                    string contraseña = Console.ReadLine();

                    if (contraseña == reader["contraseña"].ToString())
                    {
                        Usuario usuario = new Usuario(
                            reader["nombre_completo"].ToString(),
                            reader["numero_cedula"].ToString(),
                            reader["contraseña"].ToString()
                        );
                        usuario.NumeroCuenta = reader["numero_cuenta"].ToString();
                        usuario.DineroTotal = Convert.ToDecimal(reader["dinero_total"]);
                        usuario.LimiteDiarioRetiro = Convert.ToDecimal(reader["limite_diario_retiro"]);
                        usuario.LimiteDiarioRecarga = Convert.ToDecimal(reader["limite_diario_recarga"]);

                        MenuPrincipal(usuario);
                    }
                    else
                    {
                        Console.WriteLine("Contraseña incorrecta. Volviendo al menú principal.");
                    }
                }
                else
                {
                    Console.WriteLine("Cuenta no encontrada.");
                    Console.Write("¿Desea crear una cuenta? (s/n): ");
                    if (Console.ReadLine().ToLower() == "s")
                    {
                        CrearCuenta();
                    }
                    else
                    {
                        Console.Clear();
                        Console.WriteLine("Volviendo al menú principal.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al iniciar sesión: " + ex.Message);
            }
        }
    }

    static void CrearCuenta()
    {
        Console.Write("Ingrese su nombre completo: ");
        string nombreCompleto = Console.ReadLine();

        string numeroCedula;
        while (true)
        {
            Console.Write("Ingrese su número de cédula: ");
            numeroCedula = Console.ReadLine();

            if (Usuario.VerificarCedulaUnica(numeroCedula))
            {
                break;
            }
            else
            {
                Console.WriteLine("Ya existe una cuenta con esta cédula. Por favor, ingrese otra cédula.");
            }
        }

        string contraseña = "";
        while (contraseña.Length != 5)
        {
            Console.Write("Ingrese su contraseña (debe tener 5 caracteres): ");
            contraseña = Console.ReadLine();
        }

        // Generar número de cuenta
        string numeroCuenta = new Usuario(nombreCompleto, numeroCedula, contraseña).NumeroCuenta;

        // Insertar en la base de datos
        using (MySqlConnection connection = new MySqlConnection(connectionString))
        {
            try
            {
                connection.Open();
                string query = "INSERT INTO Usuario (numero_cuenta, nombre_completo, numero_cedula, contraseña, dinero_total, limite_diario_retiro, limite_diario_recarga) " +
                               "VALUES (@numeroCuenta, @nombreCompleto, @numeroCedula, @contraseña, @dineroTotal, @limiteDiarioRetiro, @limiteDiarioRecarga)";
                MySqlCommand cmd = new MySqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@numeroCuenta", numeroCuenta);
                cmd.Parameters.AddWithValue("@nombreCompleto", nombreCompleto);
                cmd.Parameters.AddWithValue("@numeroCedula", numeroCedula);
                cmd.Parameters.AddWithValue("@contraseña", contraseña);
                cmd.Parameters.AddWithValue("@dineroTotal", 0);
                cmd.Parameters.AddWithValue("@limiteDiarioRetiro", 2000000);
                cmd.Parameters.AddWithValue("@limiteDiarioRecarga", 5000000);

                cmd.ExecuteNonQuery();
                Console.Clear();
                Console.WriteLine($"Cuenta creada con éxito. Su número de cuenta es: {numeroCuenta}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al crear la cuenta: " + ex.Message);
            }
        }
    }


    static void MenuPrincipal(Usuario usuario)
    {
        Console.Clear();
        while (true)
        {
            Console.WriteLine("-------- Menú Principal --------");
            usuario.MostrarInformacion();
            Console.WriteLine("1. Retirar");
            Console.WriteLine("2. Transferencias");
            Console.WriteLine("3. Depositar");
            Console.WriteLine("6. Cerrar sesión");
            Console.Write("Seleccione una opción: ");
            int opcion = int.Parse(Console.ReadLine());

            switch (opcion)
            {
                case 1:
                    Console.Write("Ingrese la cantidad a retirar: ");
                    decimal cantidadRetiro = decimal.Parse(Console.ReadLine());
                    usuario.Retirar(cantidadRetiro);
                    break;
                case 2:
                    Console.Write("Ingrese el número de cuenta del destinatario: ");
                    string numeroCuentaDestinatario = Console.ReadLine();
                    Usuario destinatario = usuarios.Find(u => u.NumeroCuenta == numeroCuentaDestinatario);

                    if (destinatario == null)
                    {
                        Console.WriteLine("Cuenta de destinatario no encontrada. Verifique el número de cuenta.");
                    }
                    else
                    {
                        Console.Write("Ingrese el monto a transferir: ");
                        decimal montoTransferencia = decimal.Parse(Console.ReadLine());
                        usuario.Transferir(destinatario, montoTransferencia);
                    }
                    break;
                case 3:
                    Console.Write("Ingrese el monto a depositar: ");
                    if (decimal.TryParse(Console.ReadLine(), out decimal montoRecarga))
                    {
                        usuario.Recargar(montoRecarga);
                    }
                    else
                    {
                        Console.WriteLine("Cantidad inválida. Introduce un número decimal válido.");
                    }
                    break;
                case 4:
                    Console.Clear();
                    Console.WriteLine("Sesión cerrada. ¡Vuelve pronto a BANCO ORIENTE!");
                    return;
                    break;
                default:
                    Console.WriteLine("Opción no válida. Por favor, seleccione una opción válida.");
                    break;
            }
        }
    }
    
    }

    
}


