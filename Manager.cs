using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cerberus.ATC.GatewayV2Service.wsParametricas;
using Cerberus.ATC.GatewayV2Service.wsSeguridad;
using Cerberus.ATC.GatewayV2Service.wsVentas;
using NLog;
using Newtonsoft.Json;

namespace Cerberus.ATC.GatewayV2Service
{
    public class SessionData
    {
        public int Token { get; set; }
        public string Usuario { get; set; }
        public int SucursalCodigo { get; set; }

        
     
    }
    public class MtiProcessingCodeRouter
    {
        public static string RemoveNonAsciiCharString(string strInput)
        {
            return System.Text.RegularExpressions.Regex.Replace(strInput, @"[^a-zA-Z0-9.*+_{}?¿@!$%&()|°¬;:.,\/\\-]", "");
        }
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public static void ProcesarMensaje(byte[] byteRequest, out byte[] byteResponse)
        {
            Logger.Info("Trama In" + new System.Runtime.Remoting.Metadata.W3cXsd2001.SoapHexBinary(byteRequest).ToString());
            byteResponse = new byte[1];
            Logger.Trace("len mensaje:" + byteRequest.Length);
           
            byte[] mtIconsulta = new byte[] { byteRequest[7], byteRequest[8] };
            byte[] bitmap = new byte[] { byteRequest[9], byteRequest[10], byteRequest[11], byteRequest[12], byteRequest[13], byteRequest[14], byteRequest[15], byteRequest[16] };
            byte[] processingCode = new byte[] { byteRequest[17], byteRequest[18], byteRequest[19], byteRequest[20], byteRequest[21], byteRequest[22] };
            byte[] header = mtIconsulta.Concat(bitmap).Concat(processingCode).ToArray<byte>();

            byte[] newArray = new byte[byteRequest.Length - 22];
            Buffer.BlockCopy(byteRequest, 21, newArray, 0, newArray.Length);
            //byte[] newArray = new byte[byteRequest.Length - 7];
            //Buffer.BlockCopy(byteRequest, 7, newArray, 0, newArray.Length);


            int i = newArray.Length - 1;
            while (newArray[i] == 0)
                --i;
            // now foo[i] is the last non-zero byte 
            byte[] bar = new byte[i + 1];
            Array.Copy(newArray, bar, i + 1);

            string mstrHeader = BitConverter.ToString(header).Replace("-", "");
            //string mstrHeader = Encoding.ASCII.GetString(header);
            string mstrBody = BitConverter.ToString(newArray).Replace("-", "").PadLeft(10, '0');

            string mstrMessage = mstrHeader + mstrBody;
            Logger.Info("Mensaje Recibido ISO a procesar:" + mstrMessage);
            //mstrMessage = Encoding.ASCII.GetString(bar, 0, bar.Length);
            Logger.Info("Mensaje Recibido ISO a procesar:" + mstrMessage);

            SoatProcessor sp = new SoatProcessor(mstrMessage);

            string[] contenidos6263 = Encoding.ASCII.GetString(newArray).Split('\0');
            sp.Campo62 = RemoveNonAsciiCharString(contenidos6263[0]);

            sp.Campo63 = RemoveNonAsciiCharString(contenidos6263[1]);
            string strResponseBody = sp.ProcesarMensaje();

            //añadido
            byte[] tpdu = new byte[] { byteRequest[0], byteRequest[1], byteRequest[2], byteRequest[3], byteRequest[4], byteRequest[5], byteRequest[6], byteRequest[7] };
            byte[] origen = new byte[] { byteRequest[5], byteRequest[6] };
            //añadido
            // Translate the passed message into ASCII and store it as a byte array.
            Byte[] responseWPOS = new Byte[1024];
            responseWPOS = System.Runtime.Remoting.Metadata.W3cXsd2001.SoapHexBinary.Parse(strResponseBody).Value;

            responseWPOS[3] = origen[0];
            responseWPOS[4] = origen[1];
            //calculo de la longitud del mensaje
            string longitudMensajeRespuesta = (responseWPOS.Length - 2).ToString("0000");
            string decimalNumber = (responseWPOS.Length - 2).ToString("0000");
            int number = int.Parse(decimalNumber);
            string hex = number.ToString("X").PadLeft(4, '0');

            byte[] byteLongituMensaje = System.Runtime.Remoting.Metadata.W3cXsd2001.SoapHexBinary.Parse(hex).Value;
            responseWPOS[0] = byteLongituMensaje[0];
            responseWPOS[1] = byteLongituMensaje[1];
            //byteResponse = tpdu.Concat(responseWPOS).ToArray();
            byteResponse = responseWPOS;
            /*
             string strSegmentHeader = strResponseBody.Substring(1,30) ;
             byte[] headerLiteral = ConvertLiteralByteToString(strSegmentHeader);
             strResponseBody = strResponseBody.Substring(31);
             byte[] byteResponseBody = headerLiteral.Concat(Encoding.ASCII.GetBytes(strResponseBody)).ToArray(); 
             //byte[] byteHexedResBody = Encoding.Default.GetBytes(strResponseBody);
             //byte[] byteResponseBody = Encoding.ASCII.GetBytes(sp.ProcesarMensaje());
             //byteResponse = header.Concat(byteResponseBody).ToArray();
             byteResponse = byteResponseBody;
            //byteResponse = Encoding.ASCII.GetBytes(sp.ProcesarMensaje());
            */
            //comentado 0412018


        }
        public static byte[] ConvertLiteralByteToString(string str)
        {
            char[] arrC = str.ToCharArray();
            byte[] res = new byte[1024];
            return ((from c in arrC where Char.IsDigit(c) select int.Parse(c.ToString()) into convertC select BitConverter.GetBytes(convertC)[0]).ToArray());
        }
    }

    class SoatProcessor
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public const int GestionFk = 2019;
        string MsgReq { get; set; }
        string[] ParsedMsgReq { get; set; }
        string FlagMensaje { get; set; }
        string MTI { get; set; }
        public string Campo62 { get; set; }
        public string Campo63 { get; set; }
        public const int MedioPago = 29;
        public const int CanalVenta = 28;
        public SoatProcessor(string inMsgReq)
        {
            var msgIso8583 = new ISO8583();
            try
            {
                ParsedMsgReq = msgIso8583.Parse(inMsgReq);

                MsgReq = inMsgReq;
                MTI = ParsedMsgReq[0];
            }
            catch (Exception ex)
            {
                Logger.Error("Error en parseado de la trama ISO:" + inMsgReq + " Detalle del error:" + ex.Message + ex.InnerException + ex.StackTrace);

            }
        }

        public string ProcesarMensaje()
        {

            string resMensaje = "";
            ISO8583 msgIso8583 = new ISO8583();
            string hexTpdu;
            //aca el processing code y el envio a UNIVIDA Y RESPUESTA
            switch (ParsedMsgReq[3])
            {
                case "010000": //autenticacion
                    FlagMensaje = "AUT_REQ";
                    Logger.Info("Metodo de autenticacion invocado...");

                    Logger.Info("Contenido62" + this.Campo62);
                    string usuario = Campo62.Split('/')[0].Trim();
                    string password = Campo62.Split('/')[1].Trim();
                    string ip = Campo63.Trim();
                    Logger.Info(String.Format("Parametros:usuario:{0},password:{1},ip:{2}", usuario, password, ip));
                    hexTpdu = "02076000000233";
                    resMensaje = hexTpdu + Autenticacion(usuario, password, ip);



                    //messageHex = "002560064f02330110201800000a00000001000017465812053030303030303030303031323030";
                    // messageHex = "0110201800000a00000001000017465812053030303030303030303031323030"; //dani completo
                    //messageHex = "002560064f02330110201800000a00000001000017465812053030303030303030303031323030";
                    //messageHex = "00d060000002330110200000000200000601000030300013313931393139313931393139310171447c424e7c42454e495c447c43427c434f43484142414d42415c447c43487c434855515549534143415c447c4c507c4c412050415a5c477c323031387c323031395c477c323031397c323031395c557c317c504152544943554c41525c557c327c5055424c49434f5c557c337c454a45524349544f5c557c347c504f4c494349415c557c357c4f46494349414c5c567c317c4d4f544f4349434c4554415c567c327c4155544f4d4f56494c";
                    // Translate the passed message into ASCII and store it as a byte array.

                    // resMensaje = messageHex;

                    /*string[] DE = new string[130];
                    MTI = "0110";
                    DE[3] = "010000";
                    DE[39] = "010000";
                    DE[62] = "1919191919191"; //TOKEN
                    DE[63] = @"D|BN|BENI\D|CB|COCHABAMBA\D|CH|CHUQUISACA\D|LP|LA PAZ\D|OR|ORURO\D|PN|PANDO\D|PT|POTOSI\D|SC|SANTA CRUZ\D|TJ|TARIJA\U|1|PARTICULAR\U|2|PUBLICO\U|3|EJERCITO\U|4|POLICIA\U|5|OFICIAL\V|1|MOTOCICLETA\V|2|AUTOMOVIL\V|3|JEEP\V|4|CAMIONETA\V|5|VAGONETA\V|6|MICROBUS\V|7|COLECTIVO\V|8|OMNIBUS/FLOTA(MAS DE 39 oc)\V|9|TRACTO CAMION\V|10|MINIBUS(8 OCUPANTES)\V|11|MINIBUS(11 OCUPANTES)\V|12|MINIBUS(15 OCUPANTES)\V|13|CAMION(3 OCUPANTES)\V|14|CAMION(18 OCUPANTES)\V|15|CAMION(25 OCUPANTES)\";
                    resMensaje = msgIso8583.Build(DE, MTI);
                     */
                    Logger.Info("Mensaje de repuesta:" + resMensaje);
                    break;
                case "020000": //validacion de placa
                    string[] DE1 = new string[130];
                    FlagMensaje = "VAL_REQ";
                    Logger.Info("Metodo de Validacion de Placa invocado...");
                    /*MTI = "0110";
                    DE1[3] = "020000";
                    DE1[4] = "000000010000";
                    DE1[39] = "00"; //CODIGO DE RESPUESTA,00=ENROLADO Y SI VENDIBLE,10= NO ENROLADO PERO SI VENDIBLE , 01 NO VENDIBLE
                    DE1[63] = "KRS1010/01/02/05/LP";
                    hexTPDU = "02076000000233";
                    nonASCIIsection = "";
                    resMensaje = hexTPDU+msgIso8583.BuildCustomISO(DE1, MTI, out nonASCIIsection);
                     * */
                    string token = Campo62;
                    string placa = Campo63.Substring(0, 10).TrimStart('0');
                    AppDomain.CurrentDomain.SetData("token", token);
                    Logger.Info(String.Format("Parametros:token:{0},placa:{1}", token, placa));
                    hexTpdu = "02076000000233";
                    resMensaje = hexTpdu + ValidacionPlaca(token, placa);
                    //messageHex = "002f600000023301103000000002000002020000000000010000303000194b5253313031302f30312f30322f30352f4c50";

                    /*string token = ParsedMsgReq[62].Trim();
                    string placa = ParsedMsgReq[63].Trim();
                    logger.Info(String.Format("Parametros:token:{0},placa:{1}", token, placa));
                    resMensaje = ValidacionPlaca(token, placa);*/
                    break;
                case "040000": //enrolado y calculo de prima
                    string[] DE2 = new string[130];
                    FlagMensaje = "ENROL_REQ";
                    Logger.Info("Calculo de prima con placa enrolada...");
                    MTI = "0110";
                    DE2[3] = "040000";
                    DE2[4] = "000000050000"; //PRIMA A COBRAR en este caso 100.00 Bs
                    DE2[39] = "00"; //CODIGO DE RESPUESTA,00=ENROLADO Y SI VENDIBLE,10= NO ENROLADO PERO SI VENDIBLE , 01 NO VENDIBLE
                    //DE2[63] = "KRS1010/01/01/02/LP"; //PLACA/ID_GESTION/ID_VEHICULO/ID_USO/ID_DEPARTAMENTO_ EL CODIGO DE GESTION SIEMPRE 
                    //resMensaje = msgIso8583.Build(DE2, MTI);
                    hexTpdu = "001A6000000233";
                    string nonASCIIsection = "";
                    resMensaje = hexTpdu + msgIso8583.BuildCustomISO(DE2, MTI, out nonASCIIsection);
                    break;
                case "050000": //notificacion  de cobro de prima
                    string[] DE3 = new string[130];
                    FlagMensaje = "NOTIF_REQ";
                    Logger.Info("Notificacion de cobro de prima...");
                    hexTpdu = "03ae600a220000";
                    /*MTI = "0210";
                    DE3[3] = "050000";
                    DE3[39]= "00"; //CODIGO DE RESPUESTA , 00 EXITOSO, 01 ERROR
                    //DE3[62] = @"000001\00002\426801800001849\12/11/2018\000021\Ley N? 453 Tienes derecho a un trato equitativo sin discriminacion en la oferta de servicios ESTA FACTURA CONTIBUYE AL DESARROLLO DEL PAIS, EL USO ILICITO DE ESTA SER? SANCIONADO DE ACUERDO A LEY\TIPO EMISION\RAZON SOCIAL PRUEBA\3968971\SON: CIENTO VEINTISEIS CON 00/100 BOLIVIANOS\16-F2-81-21-1F\126.00\15/11/2018\426801800001849|21|15/11/2018|126.00|126.00|16-F2-81-21-1F|3968971|0.00|0.00|0.00|0.00\UNIVIDA S.A.\AV. CAMACHO ? 1425, EDIFICIO CRISPIERI NARDINI PLANTA BAJA - ZONA CENTRAL\TELEFONO 21510000 - 71561427\301204024\PLANES DE SEGUROS DE VIDA\SUCURSAL N?1\00000\LUGAR\LA PAZ- BOLIVIA\http://www.univida.bo/verificacion_soat/?p=3293275&q=xdMxyYpwyz0DeF/SU+XhdOpXlA1l4cGpOsj1G+FtLSw=\15-11-2018\000009\4949409\151515515\000009\376XRI\MINIBUS(8 OCUPANTES)\PUBLICO\DEL 1/1/2019 AL 30/12/2019\";
                    DE3[62] =   @"000001\00124\426401800015995\30/04/2019\000124\Ley N? 453 Tienes derecho a un trato equitativo sin discriminacion en la oferta de servicios ESTA FACTURA CONTIBUYE AL DESARROLLO DEL PAIS, EL USO ILICITO DE ESTA SER? SANCIONADO DE ACUERDO A LEY\TIPO EMISION\RAZON SOCIAL PRUEBA\3968971\SON: CIENTO VEINTISEIS CON 00/100 BOLIVIANOS\16-F2-81-21\126.00\15/11/2018\426801800001849|21|15/11/2018|126.00|126.00|16-F2-81-21-1F|3968971|0.00|0.00|0.00|0.00\UNIVIDA S.A.\LA PAZ\ 21510000 - 71561427\301204024\PLANES DE SEGUROS DE VIDA\SUCURSAL N?1\00000\LA PAZ- BOLIVIA\LA PAZ- BOLIVIA\http://www.univida.bo/verificacion_soat/?p=3293275&q=xdMxyYpwyz0DeF/SU+XhdOpXlA1l4cGpOsj1G+FtLSw=\15-11-2018\000010\4949409\151515515\000009\376XRI\CAMIONERA\PARTICULAR\DEL 01/01/2019 AL 30/12/2019\";

                    //DE[62] = NUMEROTRAMITE\NUMEROCOMPROBANTE\NUMEROAUTORIZACION\FECHA_LIMITE_EMISION\NUMERO_FACTURA\LEYENDA\TIPO_EMISION\RAZON_SOCIAL\NIT_CLIENTE\IMPORTE_LITERAL\CODIGO_CONTROL\IMPORTE_NUMERAL\FECHA_EMISION\CODIGOQR\RAZON_SOCIAL_UNIVIDA\DIRECCION_UNIVIDA\TELEFONOS_UNIVIDA\NIT_UNIVIDA\ACTIVIDAD_ECO_UNIVIDA\NOMBRE_SUCURSAL_UNIVIDA\DIRECCION_SUCURSAL_UNIVIDA\TELEFONO_SUCURSAL_UNIVIDA\NUMERO_SUCURSAL\LUGAR\MUNICIPIO_DEPTO
                    DE3[63] = @"SOAT (NUEVO)2019,  MINIBUS(8 OCUPANTES) PUBLICO PLACA 376XRI?1.00?126.00?126.00\";
                    //DE3[63] = @"SOAT 2019, MOTOCICLETA PARTICULAR PLACA 3677NTY?1?202,00?202,00\";
                    //DE[63] = DETALLE DE LA FACTURA EN EL SIGUIENTE ORDEN: 
                    //DETALLE?CANTIDAD?PRECIO?SUBTOTAL
                    //CADA LINEA DIVIDA POR SALTO DE LINEA
                    //hexTPDU = "02076000000233";
                     
                     resMensaje = hexTPDU+msgIso8583.BuildCustomISO(DE3, MTI, out nonASCIIsection);
                     //messageHex = "03ae600000000002102000000002000006050000303008383030303030315c30303030325c3432363830313830303030313834395c31322f31312f323031385c3030303032315c4c6579204e6f20343533205469656e6573206465726563686f206120756e20747261746f206571756974617469766f2073696e206469736372696d696e6163696f6e20656e206c61206f666572746120646520736572766963696f732045535441204641435455524120434f4e54494255594520414c204445534152524f4c4c4f2044454c20504149532c20454c2055534f20494c494349544f204445204553544120534552412053414e43494f4e41444f204445204143554552444f2041204c45595c5449504f20454d4953494f4e5c4652414e534953434f5c333936383937315c534f4e3a204349454e544f205645494e54495345495320434f4e2030302f31303020424f4c495649414e4f535c31362d46322d38312d32312d31465c3132362e30305c31352f31312f323031385c3432363830313830303030313834397c32317c31352f31312f323031387c3132362e30307c3132362e30307c31362d46322d38312d32312d31467c333936383937317c302e30307c302e30307c302e30307c302e30305c554e495649444120532e412e5c41562e2043414d4143484f204e6f20313432352c20454449464943494f20435249535049455249204e415244494e4920504c414e54412042414a41202d205a4f4e412043454e5452414c5c54454c45464f4e4f203231353130303030202d2037313536313432375c3330313230343032345c504c414e45532044452053454755524f5320444520564944415c535543555253414c204e6f20315c30303030305c4c554741525c4c412050415a2d20424f4c495649415c687474703a2f2f7777772e756e69766964612e626f2f766572696669636163696f6e5f736f61742f3f703d3332393332373526713d78644d7879597077797a304465462f53552b5868644f70586c41316c346347704f736a31472b46744c53773d5c31352d31312d323031385c3030303030395c343934393430395c3135313531353531355c3030303030395c3337365852495c4d494e494255532838204f435550414e544553295c5055424c49434f5c44454c20312f312f3230313920414c2033302f31322f323031395c0080534f415420284e5545564f29323031392c20204d494e494255532838204f435550414e54455329205055424c49434f20504c414341203337365852493f312e30303f3132362e30303f3132362e30305c";
                    //resMensaje = messageHex;
                     */

                    string terminal = ParsedMsgReq[41];
                    string fechaTransac = ParsedMsgReq[13];
                    string horaTransac = ParsedMsgReq[12];
                    string traceNumber = ParsedMsgReq[11];
                    int primaCobrada = Int32.Parse(ParsedMsgReq[4]);
                    primaCobrada = Int32.Parse(AppDomain.CurrentDomain.GetData("calculoPrima").ToString());
                    string parDeptoPcFk = AppDomain.CurrentDomain.GetData("SoatTParDepartamentoPcFk").ToString();
                    int gestionFk = Int32.Parse(AppDomain.CurrentDomain.GetData("SoatTParGestionFk").ToString());
                    int parVehiculoTipo = Int32.Parse(AppDomain.CurrentDomain.GetData("SoatTParVehiculoTipoFk").ToString()); ;
                    int parVehiculoUso = Int32.Parse(AppDomain.CurrentDomain.GetData("SoatTParVehiculoUsoFk").ToString()); ;
                    placa = AppDomain.CurrentDomain.GetData("VehiPlaca").ToString();
                    //placa = ParsedMsgReq[62];
                    token = AppDomain.CurrentDomain.GetData("token").ToString();


                    resMensaje = hexTpdu + NotificacionCobroPrima(primaCobrada,
                        Int32.Parse(token), parDeptoPcFk, gestionFk, parVehiculoTipo, parVehiculoUso, placa);





                    break;
                case "030000": //vendible no datos y calculo de prima
                    string[] DE4 = new string[130];
                    FlagMensaje = "CALC_REQ";
                    Logger.Info("Calculo de prima con placa nueva...");
                    MTI = "0110";
                    DE4[3] = "040000";
                    DE4[4] = "000000010000"; //PRIMA A COBRAR en este caso 100.00 Bs
                    DE4[39] = "00"; //CODIGO DE RESPUESTA , 00 exitoso, 01 error
                    resMensaje = msgIso8583.Build(DE4, MTI);
                    break;
                case "060000": //solicitud de listado de venta
                    string[] DE5 = new string[130];
                    FlagMensaje = "LISTA_REQ";
                    hexTpdu = "037e6000000233";
                    token = Campo62;
                    string[] strCampo63 = Campo63.Split('\\');
                    placa = "";
                    DateTime fecha = DateTime.Now;

                    /*if (strCampo63[0].Length > 0)
                    {
                        placa = strCampo63[0];
                    }
                    if (strCampo63[1].Length > 0)
                    {
                        fecha = DateTime.ParseExact(strCampo63[1], "dd-MM-yyyy", System.Globalization.CultureInfo.InvariantCulture);
                    }*/


                    /*logger.Info("Solicitud de listado de venta");
                    MTI = "0210";
                    DE5[3] = "060000";
                    DE5[39] = "00"; //CODIGO DE RESPUESTA , 00 EXITOSO, 01 ERROR
                    DE5[62] = @"00002\00002\00000"; //SOAT TOTAL\SOAT VALIDOS\SOAT REVERTIDOS SUMARIZADORES
                    DE5[63] = @"000021|KCS2933|00002|21|15-01-2019|100.00|CON COBERTURA 0A 000022|XKC2933|00003|22|15-01-2019|120.00|CON COBERTURA";
                    hexTPDU = "037e6000000233";

                     nonASCIIsection = "";
                     resMensaje = hexTPDU + msgIso8583.BuildCustomISO(DE5, MTI, out nonASCIIsection);
                     * */
                    //messageHex = "037e6000000233021020000000020000060600003030001730303030325c30303030325c303030303008533030303030317c414141313131317c30303030317c32317c31352d30312d323031397c3130302e30307c434f4e20434f424552545552415c6e3030303030327c424242323232327c30303030327c32327c31352d30312d323031397c3132302e30307c53494e20434f424552545552415c6e3030303030337c434343333333337c30303030337c32337c31352d30312d323031397c3132302e30307c434f4e20434f424552545552415c6e3030303030347c444444343434347c30303030347c32347c31352d30312d323031397c3132302e30307c53494e20434f424552545552415c6e3030303030317c414141313131317c30303030317c32317c31352d30312d323031397c3130302e30307c434f4e20434f424552545552415c6e3030303030327c424242323232327c30303030327c32327c31352d30312d323031397c3132302e30307c53494e20434f424552545552415c6e3030303030317c414141313131317c30303030317c32317c31352d30312d323031397c3130302e30307c434f4e20434f424552545552415c6e3030303030327c424242323232327c30303030327c32327c31352d30312d323031397c3132302e30307c53494e20434f424552545552415c6e3030303030337c434343333333337c30303030337c32337c31352d30312d323031397c3132302e30307c434f4e20434f424552545552415c6e3030303030347c444444343434347c30303030347c32347c31352d30312d323031397c3132302e30307c53494e20434f424552545552415c6e3030303030317c414141313131317c30303030317c32317c31352d30312d323031397c3130302e30307c434f4e20434f424552545552415c6e3030303030327c424242323232327c30303030327c32327c31352d30312d323031397c3132302e30307c53494e20434f424552545552415c6e3030303030317c414141313131317c30303030317c32317c31352d30312d323031397c3130302e30307c434f4e20434f424552545552415c6e3030303030317c414141313131317c30303030317c32317c31352d30312d323031397c3130302e30307c434f4e20434f424552545552415c6e3030303030317c414141313131317c30303030317c32317c31352d30312d323031397c3130302e30307c434f4e20434f42455254555241";
                    //resMensaje = messageHex;
                    resMensaje = hexTpdu + ListarVentas(placa, DateTime.Now, Int32.Parse(token));
                    break;
                case "070000": //solicitud de factura
                    string[] DE6 = new string[130];
                    FlagMensaje = "FACT_REQ";
                    Logger.Info("Solicitud factura");
                    hexTpdu = "03ae600a220000";
                    /*MTI = "0210";
                    DE6[3] = "070000";
                    DE6[39] = "00"; //CODIGO DE RESPUESTA , 00 EXITOSO, 01 ERROR
                    DE6[62] = @"000001\00002\426801800001849\12/11/2018\000021\Ley No 453 Tienes derecho a un trato equitativo sin discriminacion en la oferta de servicios ESTA FACTURA CONTIBUYE AL DESARROLLO DEL PAIS, EL USO ILICITO DE ESTA SERA SANCIONADO DE ACUERDO A LEY\TIPO EMISION\FRANSISCO\3968971\SON: CIENTO VEINTISEIS CON 00/100 BOLIVIANOS\16-F2-81-21-1F\126.00\15/11/2018\426801800001849|21|15/11/2018|126.00|126.00|16-F2-81-21-1F|3968971|0.00|0.00|0.00|0.00\UNIVIDA S.A.\AV. CAMACHO No 1425, EDIFICIO CRISPIERI NARDINI PLANTA BAJA - ZONA CENTRAL\TELEFONO 21510000 - 71561427\301204024\PLANES DE SEGUROS DE VIDA\SUCURSAL No 1\00000\LUGAR\LA PAZ- BOLIVIA\http://www.univida.bo/verificacion_soat/?p=3293275&q=xdMxyYpwyz0DeF/SU+XhdOpXlA1l4cGpOsj1G+FtLSw=\15-11-2018\000009\4949409\151515515\000009\376XRI\MINIBUS(8 OCUPANTES)\PUBLICO\DEL 1/1/2019 AL 30/12/2019\";
                    DE6[63] = @"SOAT 2019, MOTOCICLETA PARTICULAR PLACA 3677NTY?1?202,00?202,00\";
                    hexTPDU = "03ae600a220000";
                     nonASCIIsection = "";
                     resMensaje = hexTPDU + msgIso8583.BuildCustomISO(DE6, MTI, out nonASCIIsection);
                    //messageHex = "02df600000023302102000000002000006070000303006313030303030315c30303030325c3432363830313830303030313834395c31322f31312f323031385c3030303032315c4c6579204e6f20343533205469656e6573206465726563686f206120756e20747261746f206571756974617469766f2073696e206469736372696d696e6163696f6e20656e206c61206f666572746120646520736572766963696f732045535441204641435455524120434f4e54494255594520414c204445534152524f4c4c4f2044454c20504149532c20454c2055534f20494c494349544f204445204553544120534552412053414e43494f4e41444f204445204143554552444f2041204c45595c5449504f20454d4953494f4e5c4652414e534953434f5c333936383937315c534f4e3a204349454e544f205645494e54495345495320434f4e2030302f31303020424f4c495649414e4f535c31362d46322d38312d32312d31465c3132362e30305c31352f31312f323031385c4573746120657320756e612070727565626120646520696d70726573696f6e20646520636f6469676f2051522067656e657261646f2061206261736520646520756e6120636164656e6120646520746578746f5c554e495649444120532e412e5c41562e2043414d4143484f204e6f20313432352c20454449464943494f20435249535049455249204e415244494e4920504c414e54412042414a41202d205a4f4e412043454e5452414c5c54454c45464f4e4f203231353130303030202d2037313536313432375c3330313230343032345c504c414e45532044452053454755524f5320444520564944415c535543555253414c204e6f20325c30303030305c4c554741525c4c412050415a2d20424f4c495649415c0080534f415420284e5545564f29323031392c20204d494e494255532838204f435550414e54455329205055424c49434f20504c414341203337365852493f312e30303f3132362e30303f3132362e30305c";
                    //resMensaje = messageHex;
                     * */
                    //placa = ParsedMsgReq[62];
                    string comprobante = new String(Campo63.Where(Char.IsDigit).ToArray());
                    if (comprobante.Length > 7) { comprobante = comprobante.Substring(0, 7); }
                    //comprobante = "3097211";
                    token = Campo62;
                    resMensaje = hexTpdu + ObtenerFacturaComprobante(Int32.Parse(token),
                        Int32.Parse(comprobante));


                    break;

            }
            return resMensaje;
        }
        public string ObtenerFacturaComprobante(int token, int comprobante)
        {
            ISO8583 msgIso8583 = new ISO8583();
            string resMensaje = "";
            int gestionFk = 2019;
            string usuario = "dquenallata";
            IwsVentasClient ventasClient = new IwsVentasClient();
            CEVen05Obtener reqCEVen05Obtener = new CEVen05Obtener();
            reqCEVen05Obtener.FactAutorizacionNumero = "";
            reqCEVen05Obtener.FactNumero = 0;
            reqCEVen05Obtener.SeguridadToken = token;
            reqCEVen05Obtener.SoatNroComprobante = comprobante;
            reqCEVen05Obtener.SoatTIntermediarioFk = 0;
            reqCEVen05Obtener.SoatTParGestionFk = gestionFk;
            reqCEVen05Obtener.SoatTParVentaCanalFk = CanalVenta;
            reqCEVen05Obtener.SoatVentaCajero = "";
            reqCEVen05Obtener.SoatVentaVendedor = usuario;
            reqCEVen05Obtener.Usuario = usuario;
            reqCEVen05Obtener.VehiPlaca = "";
            Logger.Info("llamado Ven05Obtener , objeto req" + JsonConvert.SerializeObject(reqCEVen05Obtener));
            CSVen05Obtener resCSVen05Obtener = ventasClient.Ven05Obtener(reqCEVen05Obtener);
            Logger.Info("llamado Ven05Obtener , objeto res" + JsonConvert.SerializeObject(resCSVen05Obtener));
            if (resCSVen05Obtener.Exito)
            {
                CSoatDatosCompletosFactura datosCompletosFactura = resCSVen05Obtener.oSoatDatosCompletosFactura;
                CFacturaMaestro datosFacturaMaestro = resCSVen05Obtener.oSoatDatosCompletosFactura.oFacturaMaestro;
                string[] DE3 = new string[130];
                FlagMensaje = "NOTIF_REQ";
                Logger.Info("Armado de mensaje para:" + FlagMensaje);
                string leyenda = "Ley No 453 Tienes derecho a un trato equitativo sin discriminacion en la oferta de servicios ESTA FACTURA CONTIBUYE AL DESARROLLO DEL PAIS, EL USO ILICITO DE ESTA SERA SANCIONADO DE ACUERDO A LEY";
                object[] param62 = new object[]{
                datosFacturaMaestro.NumeroTramite,datosFacturaMaestro.NumeroFactura /*datosCompletosFactura.SoatNroComprobante*/,datosFacturaMaestro.NumeroAutorizacion,datosFacturaMaestro.FechaLimiteEmision,
               datosFacturaMaestro.NumeroFactura,/*datosFacturaMaestro.Leyenda.Replace('°','o')*/ leyenda,"EMISION",datosFacturaMaestro.RazonSocialCliente,datosFacturaMaestro.NitCiCliente,datosFacturaMaestro.ImporteLiteral,
               datosFacturaMaestro.CodigoControl,datosFacturaMaestro.ImporteNumeral,datosFacturaMaestro.FechaEmision,datosFacturaMaestro.CodigoQR,
               datosFacturaMaestro.RazonSocial,datosFacturaMaestro.DireccionEmpresa,datosFacturaMaestro.TefefonosEmpresa,datosFacturaMaestro.NitEmpresa,
               datosFacturaMaestro.ActividadEconomica,datosFacturaMaestro.NombreSucursal,//datosFacturaMaestro.DireccionSucursal,datosFacturaMaestro.TelefonoSucursal,
               datosFacturaMaestro.NumeroSucursal,datosFacturaMaestro.Lugar,datosFacturaMaestro.MunicipioDepartamento,/*resCsVen03EfectivizarFactCicl.oSoatDatosCompletosFactura.SoatQRContenido*/"http://www.univida.bo/verificacion_soat/?p=3293275&q=xdMxyYpwyz0DeF/SU+XhdOpXlA1l4cGpOsj1G+FtLSw=",
               datosFacturaMaestro.FechaEmision,datosFacturaMaestro.NumeroFactura/*datosCompletosFactura.SoatNroComprobante*/,datosCompletosFactura.SoatRosetaNumero,datosCompletosFactura.SoatRosetaNumero,datosFacturaMaestro.NumeroFactura,
               datosCompletosFactura.VehiPlaca,datosCompletosFactura.SoatTParVehiculoTipoDescripcion,datosCompletosFactura.SoatTParVehiculoUsoDescripcion,
               "Del 01/01/2019 al 31/12/2019"
               //String.Format("Del {0} al {1}",datosCompletosFactura.SoatFechaCoberturaInicio.ToString("dd/MM/yyyy"),datosCompletosFactura.SoatFechaCoberturaFin.ToString("dd/MM/yyyy"))
                };
                string dumpObjectReq = JsonConvert.SerializeObject(datosCompletosFactura, Formatting.Indented);
                string dumpOFacturaMaestro = JsonConvert.SerializeObject(datosFacturaMaestro, Formatting.Indented);
                Logger.Info("ObjDatosCompletosFactura:\n" + dumpObjectReq);
                Logger.Info("ObjdatosFacturaMaestro:\n" + dumpOFacturaMaestro);
                string parametrosCampo62 = String.Format(@"{0}\{1}\{2}\{3}\{4}\{5}\{6}\{7}\{8}\{9}\{10}\{11}\{12}\{13}\{14}\{15}\{16}\{17}\{18}\{19}\{20}\{21}\{22}\{23}\{24}\{25}\{26}\{27}\{28}\{29}\{30}\{31}\{32}\", param62);
                //parametrosCampo62 = removeNonASCIIChar(parametrosCampo62);
                Logger.Info("Parametros Enviados campo62:" + parametrosCampo62);
                MTI = "0210";
                DE3[3] = "070000";
                DE3[39] = "00"; //CODIGO DE RESPUESTA , 00 EXITOSO, 01 ERROR
                DE3[62] = parametrosCampo62;
                //DE3[62] = @"000001\00002\426801800001849\12/11/2018\000021\Ley N? 453 Tienes derecho a un trato equitativo sin discriminacion en la oferta de servicios ESTA FACTURA CONTIBUYE AL DESARROLLO DEL PAIS, EL USO ILICITO DE ESTA SER? SANCIONADO DE ACUERDO A LEY\TIPO EMISION\RAZON SOCIAL PRUEBA\3968971\SON: CIENTO VEINTISEIS CON 00/100 BOLIVIANOS\16-F2-81-21-1F\126.00\15/11/2018\426801800001849|21|15/11/2018|126.00|126.00|16-F2-81-21-1F|3968971|0.00|0.00|0.00|0.00\UNIVIDA S.A.\AV. CAMACHO ? 1425, EDIFICIO CRISPIERI NARDINI PLANTA BAJA - ZONA CENTRAL\TELEFONO 21510000 - 71561427\301204024\PLANES DE SEGUROS DE VIDA\SUCURSAL N?1\00000\LUGAR\LA PAZ- BOLIVIA\http://www.univida.bo/verificacion_soat/?p=3293275&q=xdMxyYpwyz0DeF/SU+XhdOpXlA1l4cGpOsj1G+FtLSw=\15-11-2018\000009\4949409\151515515\000009\376XRI\MINIBUS(8 OCUPANTES)\PUBLICO\DEL 1/1/2019 AL 30/12/2019\";
                //DE[62] = NUMEROTRAMITE\NUMEROCOMPROBANTE\NUMEROAUTORIZACION\FECHA_LIMITE_EMISION\NUMERO_FACTURA\LEYENDA\TIPO_EMISION\RAZON_SOCIAL\NIT_CLIENTE\IMPORTE_LITERAL\CODIGO_CONTROL\IMPORTE_NUMERAL\FECHA_EMISION\CODIGOQR\RAZON_SOCIAL_UNIVIDA\DIRECCION_UNIVIDA\TELEFONOS_UNIVIDA\NIT_UNIVIDA\ACTIVIDAD_ECO_UNIVIDA\NOMBRE_SUCURSAL_UNIVIDA\DIRECCION_SUCURSAL_UNIVIDA\TELEFONO_SUCURSAL_UNIVIDA\NUMERO_SUCURSAL\LUGAR\MUNICIPIO_DEPTO\QR comprobante\Fecha de emisión\Numero de comprobante\Numero de roseta\Numero de factura\Placa\Tipo de vehiculo\Tipo de Uso\Vigencia de cobertura SOAT
                string detalleFactura = "";
                foreach (var detalle in datosFacturaMaestro.LFacturaDetalle)
                {
                    detalleFactura += String.Format(@"{0}?{1}?{2}?{3}\", detalle.LineaDetalle, detalle.Cantidad, detalle.ImporteUnitario, detalle.ImporteTotal);

                }
                Logger.Info("Parametros Enviados campo63:" + detalleFactura);
                DE3[63] = System.Text.Encoding.ASCII.GetString(System.Text.Encoding.ASCII.GetBytes(detalleFactura));
                //DE3[63] = @"SOAT (NUEVO)2019,  MINIBUS(8 OCUPANTES) PUBLICO PLACA 376XRI?1.00?126.00?126.00\";
                //DE[63] = DETALLE DE LA FACTURA EN EL SIGUIENTE ORDEN: 
                //DETALLE?CANTIDAD?PRECIO?SUBTOTAL
                Logger.Info("Exito en la obtencion de factura:" + resCSVen05Obtener.CodigoRetorno + " mensaje:" + resCSVen05Obtener.Mensaje);

                string nonASCIIsection = "";
                resMensaje = msgIso8583.BuildCustomISO(DE3, MTI, out nonASCIIsection);

            }
            else
            {
                string[] messageRespuesta = new string[130];
                MTI = "210";
                messageRespuesta[3] = "070000";
                messageRespuesta[39] = "01"; //CODIGO DE RESPUESTA , 00 EXITOSO, 01 ERROR
                resMensaje = msgIso8583.Build(messageRespuesta, MTI);
                Logger.Error("Error en la obtencion de la factura:" + resCSVen05Obtener.Mensaje + resCSVen05Obtener.CodigoRetorno);

            }
            return resMensaje;


        }
        public string ListarVentas(string placa, DateTime fecha, int token)
        {
            placa = "";
            fecha = DateTime.Now;
            ISO8583 msgIso8583 = new ISO8583();
            string resMensaje = "";
            int gestionFk = 2019;
            string usuario = "dquenallata";
            IwsVentasClient clientVentas = new IwsVentasClient();
            CEVen04Listar reqCEVen04Listar = new CEVen04Listar();
            reqCEVen04Listar.SeguridadToken = token;
            reqCEVen04Listar.SoatTIntermediarioFk = 0;
            reqCEVen04Listar.SoatTParGestionFk = 2019;
            reqCEVen04Listar.SoatTParVentaCanalFk = CanalVenta;
            reqCEVen04Listar.SoatVentaFecha = DateTime.Now;
            reqCEVen04Listar.SoatVentaVendedor = usuario;
            reqCEVen04Listar.Usuario = usuario;
            reqCEVen04Listar.VehiPlaca = "";
            Logger.Info("llamado de listado de ventas:" + JsonConvert.SerializeObject(reqCEVen04Listar));
            CSVen04Listar resCSVen04Listar = clientVentas.Ven04Listar(reqCEVen04Listar);
            Logger.Info("resultado de llamado de listado de ventas:" + JsonConvert.SerializeObject(resCSVen04Listar));
            if (resCSVen04Listar.Exito)
            {
                CRcvDatosVentas datosVentas = resCSVen04Listar.oRcvDatosVentas;
                string[] DE5 = new string[130];

                MTI = "0210";
                DE5[3] = "060000";
                DE5[39] = "00"; //CODIGO DE RESPUESTA , 00 EXITOSO, 01 ERROR
                DE5[62] = String.Format(@"{0}\{1}\{2}", datosVentas.RcvCantidadSoat, datosVentas.RcvCantidadSoatValidos, datosVentas.RcvCantidadSoatAnulados + datosVentas.RcvCantidadSoatRevertidos); //SOAT TOTAL\SOAT VALIDOS\SOAT REVERTIDOS SUMARIZADORES
                string strDetalleVentas = "";
                foreach (var detalleVenta in datosVentas.lSoatDatosVenta)
                {
                    strDetalleVentas += (String.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}\\n", new object[] { detalleVenta.SoatNroComprobante, detalleVenta.VehiPlaca, detalleVenta.FactAutorizacionNumero, detalleVenta.FactNumero, detalleVenta.FactFecha, detalleVenta.FactPrima, detalleVenta.SoatTParGenericaEstadoFk.ToString() }));
                }
                DE5[63] = strDetalleVentas;
                string nonASCIIsection = "";

                resMensaje = msgIso8583.BuildCustomISO(DE5, MTI, out nonASCIIsection);

            }
            else
            {
                string[] messageRespuesta = new string[130];
                MTI = "0210";
                messageRespuesta[3] = "060000";
                messageRespuesta[39] = "01"; //CODIGO DE RESPUESTA , 00 EXITOSO, 01 ERROR
                resMensaje = msgIso8583.Build(messageRespuesta, MTI);
                Logger.Error("Error obtencion listado de ventas :" + resCSVen04Listar.Mensaje + resCSVen04Listar.CodigoRetorno);
            }

            return resMensaje;
        }
        public static string removeNonASCIIChar(string inputText)
        {

            string asAscii = Encoding.ASCII.GetString(
                Encoding.Convert(
                    Encoding.UTF8,
                    Encoding.GetEncoding(
                        Encoding.ASCII.EncodingName,
                        new EncoderReplacementFallback(string.Empty),
                        new DecoderExceptionFallback()
                        ),
                    Encoding.UTF8.GetBytes(inputText)
                )
            );
            return asAscii;
        }
        public string NotificacionCobroPrima(int primaCobrar, int token, string parDeptoPcFk, int gestionFk, int parVehiculoTipo, int parVehiculoUso, string placa)
        {
            primaCobrar = primaCobrar / 100;
            string resMensaje = "";
            string nitCi = "0";
            string razonSocial = "S/N";
            gestionFk = 2019;
            //int sucursalCodigo = Int32.Parse(AppDomain.CurrentDomain.GetData("SucursalCodigo").ToString());
            int sucursalCodigo = 10101;
            string usuario = "dquenallata";
            ISO8583 msgIso8583 = new ISO8583();
            //efectivizacion de factura
            CEVen03EfectivizarFactCicl reqCeVen03EfectivizarFactCicl = new CEVen03EfectivizarFactCicl();
            reqCeVen03EfectivizarFactCicl.FactCorreoCliente = "";
            reqCeVen03EfectivizarFactCicl.FactNitCi = "";
            reqCeVen03EfectivizarFactCicl.FactPrima = primaCobrar;
            reqCeVen03EfectivizarFactCicl.FactRazonSocial = "";
            reqCeVen03EfectivizarFactCicl.FactSucursalFk = sucursalCodigo;
            reqCeVen03EfectivizarFactCicl.FactTelefonoCliente = "";
            reqCeVen03EfectivizarFactCicl.SeguridadToken = token;
            reqCeVen03EfectivizarFactCicl.SoatRosetaNumero = 0;
            reqCeVen03EfectivizarFactCicl.SoatTIntermediarioFk = 0;
            reqCeVen03EfectivizarFactCicl.SoatTParDepartamentoPcFk = parDeptoPcFk;
            reqCeVen03EfectivizarFactCicl.SoatTParDepartamentoVtFk = parDeptoPcFk;
            reqCeVen03EfectivizarFactCicl.SoatTParGestionFk = gestionFk;
            reqCeVen03EfectivizarFactCicl.SoatTParMedioPagoFk = MedioPago;
            reqCeVen03EfectivizarFactCicl.SoatTParVehiculoTipoFk = parVehiculoTipo;
            reqCeVen03EfectivizarFactCicl.SoatTParVehiculoUsoFk = parVehiculoUso;
            reqCeVen03EfectivizarFactCicl.SoatTParVentaCanalFk = CanalVenta;
            reqCeVen03EfectivizarFactCicl.SoatVentaCajero = "";
            reqCeVen03EfectivizarFactCicl.SoatVentaDatosAdi = "VENTA POR POS ATC";
            reqCeVen03EfectivizarFactCicl.SoatVentaVendedor = usuario;
            reqCeVen03EfectivizarFactCicl.Usuario = usuario;
            reqCeVen03EfectivizarFactCicl.VehiPlaca = placa;
            IwsVentasClient clientVentasClient = new IwsVentasClient();
            Logger.Info("Llamando a notificacion de venta Ven03EfectivizarFactCicl " + JsonConvert.SerializeObject(reqCeVen03EfectivizarFactCicl, Formatting.Indented));
            var resCsVen03EfectivizarFactCicl = clientVentasClient.Ven03EfectivizarFactCicl(reqCeVen03EfectivizarFactCicl);
            Logger.Info("respuesta de Ven03EfectivizarFactCicl:" + resCsVen03EfectivizarFactCicl.Mensaje);
            if (resCsVen03EfectivizarFactCicl.Exito)
            {
                CSoatDatosCompletosFactura datosCompletosFactura = resCsVen03EfectivizarFactCicl.oSoatDatosCompletosFactura;
                CFacturaMaestro datosFacturaMaestro = resCsVen03EfectivizarFactCicl.oSoatDatosCompletosFactura.oFacturaMaestro;
                string[] DE3 = new string[130];
                FlagMensaje = "NOTIF_REQ";
                Logger.Info("Armado de mensaje para:" + FlagMensaje);
                string leyenda = "Ley No 453 Tienes derecho a un trato equitativo sin discriminacion en la oferta de servicios ESTA FACTURA CONTIBUYE AL DESARROLLO DEL PAIS, EL USO ILICITO DE ESTA SERA SANCIONADO DE ACUERDO A LEY";
                object[] param62 = new object[]{
                datosFacturaMaestro.NumeroTramite,datosFacturaMaestro.NumeroFactura /*datosCompletosFactura.SoatNroComprobante*/,datosFacturaMaestro.NumeroAutorizacion,datosFacturaMaestro.FechaLimiteEmision,
               datosFacturaMaestro.NumeroFactura,/*datosFacturaMaestro.Leyenda.Replace('°','o')*/ leyenda,"EMISION",razonSocial,nitCi,datosFacturaMaestro.ImporteLiteral,
               datosFacturaMaestro.CodigoControl,datosFacturaMaestro.ImporteNumeral,datosFacturaMaestro.FechaEmision,datosFacturaMaestro.CodigoQR,
               datosFacturaMaestro.RazonSocial,datosFacturaMaestro.DireccionEmpresa,datosFacturaMaestro.TefefonosEmpresa,datosFacturaMaestro.NitEmpresa,
               datosFacturaMaestro.ActividadEconomica,datosFacturaMaestro.NombreSucursal,//datosFacturaMaestro.DireccionSucursal,datosFacturaMaestro.TelefonoSucursal,
               datosFacturaMaestro.NumeroSucursal,datosFacturaMaestro.Lugar,datosFacturaMaestro.MunicipioDepartamento,/*resCsVen03EfectivizarFactCicl.oSoatDatosCompletosFactura.SoatQRContenido*/"http://www.univida.bo/verificacion_soat/?p=3293275&q=xdMxyYpwyz0DeF/SU+XhdOpXlA1l4cGpOsj1G+FtLSw=",
               datosFacturaMaestro.FechaEmision,datosFacturaMaestro.NumeroFactura/*datosCompletosFactura.SoatNroComprobante*/,datosCompletosFactura.SoatRosetaNumero,datosCompletosFactura.SoatRosetaNumero,datosFacturaMaestro.NumeroFactura,
               datosCompletosFactura.VehiPlaca,datosCompletosFactura.SoatTParVehiculoTipoDescripcion,datosCompletosFactura.SoatTParVehiculoUsoDescripcion,
               "Del 01/01/2019 al 31/12/2019"
               //String.Format("Del {0} al {1}",datosCompletosFactura.SoatFechaCoberturaInicio.ToString("dd/MM/yyyy"),datosCompletosFactura.SoatFechaCoberturaFin.ToString("dd/MM/yyyy"))
                };
                string dumpObjectReq = JsonConvert.SerializeObject(datosCompletosFactura, Formatting.Indented);
                string dumpOFacturaMaestro = JsonConvert.SerializeObject(datosFacturaMaestro, Formatting.Indented);
                Logger.Info("ObjDatosCompletosFactura:\n" + dumpObjectReq);
                Logger.Info("ObjdatosFacturaMaestro:\n" + dumpOFacturaMaestro);
                string parametrosCampo62 = String.Format(@"{0}\{1}\{2}\{3}\{4}\{5}\{6}\{7}\{8}\{9}\{10}\{11}\{12}\{13}\{14}\{15}\{16}\{17}\{18}\{19}\{20}\{21}\{22}\{23}\{24}\{25}\{26}\{27}\{28}\{29}\{30}\{31}\{32}\", param62);
                //parametrosCampo62 = removeNonASCIIChar(parametrosCampo62);
                Logger.Info("Parametros Enviados campo62:" + parametrosCampo62);
                MTI = "0210";
                DE3[3] = "050000";
                DE3[39] = "00"; //CODIGO DE RESPUESTA , 00 EXITOSO, 01 ERROR
                DE3[62] = parametrosCampo62;
                //DE3[62] = @"000001\00002\426801800001849\12/11/2018\000021\Ley N? 453 Tienes derecho a un trato equitativo sin discriminacion en la oferta de servicios ESTA FACTURA CONTIBUYE AL DESARROLLO DEL PAIS, EL USO ILICITO DE ESTA SER? SANCIONADO DE ACUERDO A LEY\TIPO EMISION\RAZON SOCIAL PRUEBA\3968971\SON: CIENTO VEINTISEIS CON 00/100 BOLIVIANOS\16-F2-81-21-1F\126.00\15/11/2018\426801800001849|21|15/11/2018|126.00|126.00|16-F2-81-21-1F|3968971|0.00|0.00|0.00|0.00\UNIVIDA S.A.\AV. CAMACHO ? 1425, EDIFICIO CRISPIERI NARDINI PLANTA BAJA - ZONA CENTRAL\TELEFONO 21510000 - 71561427\301204024\PLANES DE SEGUROS DE VIDA\SUCURSAL N?1\00000\LUGAR\LA PAZ- BOLIVIA\http://www.univida.bo/verificacion_soat/?p=3293275&q=xdMxyYpwyz0DeF/SU+XhdOpXlA1l4cGpOsj1G+FtLSw=\15-11-2018\000009\4949409\151515515\000009\376XRI\MINIBUS(8 OCUPANTES)\PUBLICO\DEL 1/1/2019 AL 30/12/2019\";
                //DE[62] = NUMEROTRAMITE\NUMEROCOMPROBANTE\NUMEROAUTORIZACION\FECHA_LIMITE_EMISION\NUMERO_FACTURA\LEYENDA\TIPO_EMISION\RAZON_SOCIAL\NIT_CLIENTE\IMPORTE_LITERAL\CODIGO_CONTROL\IMPORTE_NUMERAL\FECHA_EMISION\CODIGOQR\RAZON_SOCIAL_UNIVIDA\DIRECCION_UNIVIDA\TELEFONOS_UNIVIDA\NIT_UNIVIDA\ACTIVIDAD_ECO_UNIVIDA\NOMBRE_SUCURSAL_UNIVIDA\DIRECCION_SUCURSAL_UNIVIDA\TELEFONO_SUCURSAL_UNIVIDA\NUMERO_SUCURSAL\LUGAR\MUNICIPIO_DEPTO\QR comprobante\Fecha de emisión\Numero de comprobante\Numero de roseta\Numero de factura\Placa\Tipo de vehiculo\Tipo de Uso\Vigencia de cobertura SOAT
                string detalleFactura = "";
                foreach (var detalle in datosFacturaMaestro.LFacturaDetalle)
                {
                    detalleFactura += String.Format(@"{0}?{1}?{2}?{3}\", detalle.LineaDetalle, detalle.Cantidad, detalle.ImporteUnitario, detalle.ImporteTotal);

                }
                Logger.Info("Parametros Enviados campo63:" + detalleFactura);
                DE3[63] = System.Text.Encoding.ASCII.GetString(System.Text.Encoding.ASCII.GetBytes(detalleFactura));
                //DE3[63] = @"SOAT (NUEVO)2019,  MINIBUS(8 OCUPANTES) PUBLICO PLACA 376XRI?1.00?126.00?126.00\";
                //DE[63] = DETALLE DE LA FACTURA EN EL SIGUIENTE ORDEN: 
                //DETALLE?CANTIDAD?PRECIO?SUBTOTAL
                Logger.Info("Exito en la venta:" + resCsVen03EfectivizarFactCicl.CodigoRetorno + " mensaje:" + resCsVen03EfectivizarFactCicl.Mensaje);

                string nonASCIIsection = "";
                resMensaje = msgIso8583.BuildCustomISO(DE3, MTI, out nonASCIIsection);


            }
            else
            {
                string[] messageRespuesta = new string[130];
                MTI = "2110";
                messageRespuesta[3] = "050000";
                messageRespuesta[39] = "01"; //CODIGO DE RESPUESTA , 00 EXITOSO, 01 ERROR
                resMensaje = msgIso8583.Build(messageRespuesta, MTI);
                Logger.Error("Error en la efectivizacion factura:" + resCsVen03EfectivizarFactCicl.Mensaje + resCsVen03EfectivizarFactCicl.CodigoRetorno);


            }
            return resMensaje;
        }
        public int CalculoPrima(int token, string parDeptoPcFk, int gestionFk, int parVehiculoTipo, int parVehiculoUso, int canalVenta, string usuario, string placa)
        {
            int calculoPrima = 0;
            IwsVentasClient clientVentasClient = new IwsVentasClient();
            CEVen02ObtenerPrima reqCEVen02ObtenerPrima = new CEVen02ObtenerPrima();
            reqCEVen02ObtenerPrima.SeguridadToken = token;
            reqCEVen02ObtenerPrima.SoatTIntermediarioFk = 0;
            reqCEVen02ObtenerPrima.SoatTParDepartamentoPcFk = parDeptoPcFk;
            reqCEVen02ObtenerPrima.SoatTParGestionFk = gestionFk;
            reqCEVen02ObtenerPrima.SoatTParVehiculoTipoFk = parVehiculoTipo;
            reqCEVen02ObtenerPrima.SoatTParVehiculoUsoFk = parVehiculoUso;
            reqCEVen02ObtenerPrima.SoatTParVentaCanalFk = canalVenta;
            reqCEVen02ObtenerPrima.SoatVentaCajero = "";
            reqCEVen02ObtenerPrima.SoatVentaVendedor = usuario;
            reqCEVen02ObtenerPrima.VehiPlaca = placa;
            reqCEVen02ObtenerPrima.Usuario = usuario;
            Logger.Info("Llamada Metodo de calculo de prima," + JsonConvert.SerializeObject(reqCEVen02ObtenerPrima, Formatting.Indented));
            CSVen02ObtenerPrima resCsVen02ObtenerPrima = clientVentasClient.Ven02ObtenerPrima(reqCEVen02ObtenerPrima);
            Logger.Info("Llamada Metodo de calculo de prima,resultado:" + JsonConvert.SerializeObject(resCsVen02ObtenerPrima));
            if (resCsVen02ObtenerPrima.Exito)
            {
                calculoPrima = resCsVen02ObtenerPrima.Prima;
            }
            else
            {
                Logger.Error("Error en calculo de prima,finalizando");

            }

            return calculoPrima * 100;
        }
        string EnroladoCalculoPrima(string token, string placa, int idGestion, int idTipo, int idUso, string idDepto)
        {
            string resMensaje = "";
            ISO8583 msgIso8583 = new ISO8583();
            IwsVentasClient client = new IwsVentasClient();
            CEVen02ObtenerPrima reqCEVen02ObtenerPrima = new CEVen02ObtenerPrima();
            reqCEVen02ObtenerPrima.SeguridadToken = Int32.Parse(token);
            reqCEVen02ObtenerPrima.SoatTIntermediarioFk = 0;
            reqCEVen02ObtenerPrima.SoatTParDepartamentoPcFk = idDepto;
            reqCEVen02ObtenerPrima.SoatTParGestionFk = idGestion;
            reqCEVen02ObtenerPrima.SoatTParVehiculoTipoFk = idTipo;
            reqCEVen02ObtenerPrima.SoatTParVehiculoUsoFk = idUso;
            reqCEVen02ObtenerPrima.SoatTParVentaCanalFk = 30;
            reqCEVen02ObtenerPrima.SoatVentaCajero = "EDWIN";
            reqCEVen02ObtenerPrima.SoatVentaVendedor = "EDWIN";
            reqCEVen02ObtenerPrima.Usuario = "EDWIN";
            reqCEVen02ObtenerPrima.VehiPlaca = placa;
            Logger.Info("llamada metodo Ven02ObtenerPrima");
            CSVen02ObtenerPrima resCSVen02ObtenerPrima = client.Ven02ObtenerPrima(reqCEVen02ObtenerPrima);
            Logger.Info("Respuesta metodo Ven02ObtenerPrima" + resCSVen02ObtenerPrima.Mensaje);
            if (resCSVen02ObtenerPrima.Exito)
            {
                string[] messageRespuesta = new string[130];
                MTI = "0110";
                messageRespuesta[3] = "030000";
                messageRespuesta[4] = resCSVen02ObtenerPrima.Prima.ToString();
                messageRespuesta[3] = "030000";
                messageRespuesta[39] = "00"; //CODIGO DE RESPUESTA,00=ENROLADO Y SI VENDIBLE,10= NO ENROLADO PERO SI VENDIBLE , 01 NO VENDIBLE


                resMensaje = msgIso8583.Build(messageRespuesta, MTI);
                Logger.Info("mensaje enviado:" + resMensaje);
            }
            return resMensaje;

        }
        string ValidacionPlaca(string token, string placa)
        {
            string usuario = "";
            string mensajeError = "ERROR DESCONOCIDO,INTENTE NUEVAMENTE";
            
            string resMensaje = "";
            ISO8583 msgIso8583 = new ISO8583();
            IwsVentasClient client = new IwsVentasClient();
            try
            {
                List<SessionData> datosSesion = (List<SessionData>)AppDomain.CurrentDomain.GetData("SessionData");
                if (datosSesion == null)
                {
                    throw new Exception("Sesion incorrecta");
                }

                SessionData first = null;
                foreach (var d in datosSesion)
                {
                    if (d.Token == Int32.Parse(token))
                    {
                        first = d;
                        break;
                    }
                }

                if (first != null){ usuario = first.Usuario;}
                else
                {
                    throw new Exception("Usuario no encontrado");
                }

                CEVen01ValidarVendibleYObtenerDatos reqCeVen01ValidarVendibleYObtenerDatos = new CEVen01ValidarVendibleYObtenerDatos();
                reqCeVen01ValidarVendibleYObtenerDatos.SeguridadToken = int.Parse(token);
                reqCeVen01ValidarVendibleYObtenerDatos.SoatTIntermediarioFk = 0;
                reqCeVen01ValidarVendibleYObtenerDatos.SoatTParGestionFk = GestionFk;
                reqCeVen01ValidarVendibleYObtenerDatos.SoatTParVentaCanalFk = CanalVenta;
                reqCeVen01ValidarVendibleYObtenerDatos.SoatVentaCajero = "";
                reqCeVen01ValidarVendibleYObtenerDatos.SoatVentaVendedor = usuario;
                reqCeVen01ValidarVendibleYObtenerDatos.Usuario = usuario;
                reqCeVen01ValidarVendibleYObtenerDatos.VehiPlaca = placa;
                Logger.Info("llamada metodo Ven01ValidarVendibleYObtenerDatos," + JsonConvert.SerializeObject(reqCeVen01ValidarVendibleYObtenerDatos));
                CSVen01ValidarVendibleYObtenerDatos resCsVen01ValidarVendibleYObtenerDatos =
                    client.Ven01ValidarVendibleYObtenerDatos(reqCeVen01ValidarVendibleYObtenerDatos);
                Logger.Info("Respuesta metodo Ven01ValidarVendibleYObtenerDatos" + JsonConvert.SerializeObject(resCsVen01ValidarVendibleYObtenerDatos.Mensaje));
                mensajeError = resCsVen01ValidarVendibleYObtenerDatos.Mensaje;
                if (resCsVen01ValidarVendibleYObtenerDatos.Exito)
                {
                    AppDomain.CurrentDomain.SetData("VehiPlaca", placa);
                    if (resCsVen01ValidarVendibleYObtenerDatos.oSoatDatosIniciales == null) //es vendible pero no esta enrolado
                    {
                        string[] messageRespuesta = new string[130];
                        MTI = "0110";
                        messageRespuesta[3] = "030000";

                        messageRespuesta[39] = "10"; //CODIGO DE RESPUESTA,00=ENROLADO Y SI VENDIBLE,10= NO ENROLADO PERO SI VENDIBLE , 01 NO VENDIBLE
                        string nonASCIIsection = "";
                        //messageRespuesta[63] = "mkk737/1/1/1/1";
                        resMensaje = msgIso8583.BuildCustomISO(messageRespuesta, MTI, out nonASCIIsection);
                    }
                    else //es vendible y esta enrolado
                    {
                        CSoatDatosIniciales datosIniciales = resCsVen01ValidarVendibleYObtenerDatos.oSoatDatosIniciales;
                        int calculoPrima = CalculoPrima(
                            Int32.Parse(token), datosIniciales.SoatTParDepartamentoPcFk, GestionFk,
                            datosIniciales.SoatTParVehiculoTipoFk, datosIniciales.SoatTParVehiculoUsoFk, CanalVenta,
                            usuario, placa);
                        if (calculoPrima <= 0)
                        {
                            string dumpObjectReq = JsonConvert.SerializeObject(datosIniciales, Formatting.None);
                            throw new Exception("Prima no calculada,0,dump objetoDatosIniciales:" + dumpObjectReq);
                        }
                        string[] messageRespuesta = new string[130];
                        MTI = "0110";
                        messageRespuesta[3] = "030000";
                        messageRespuesta[4] = calculoPrima.ToString().PadLeft(13, '0');

                        messageRespuesta[39] = "10"; //CODIGO DE RESPUESTA,00=ENROLADO Y SI VENDIBLE,10= NO ENROLADO PERO SI VENDIBLE , 01 NO VENDIBLE
                        //messageRespuesta[63] = String.Format("{0}/{1}/{2}/{3}/{4}", datosIniciales.VehiPlaca, datosIniciales.SoatTParGestionFk, datosIniciales.SoatTParVehiculoTipoFk, datosIniciales.SoatTParVehiculoUsoFk, datosIniciales.SoatTParDepartamentoPcFk);
                        string nonASCIIsection = "";
                        resMensaje = msgIso8583.BuildCustomISO(messageRespuesta, MTI, out nonASCIIsection);
                        /*AppDomain.CurrentDomain.SetData("SoatTParGestionFk", datosIniciales.SoatTParGestionFk);
                        AppDomain.CurrentDomain.SetData("calculoPrima", calculoPrima);
                        AppDomain.CurrentDomain.SetData("SoatTParVehiculoUsoFk", datosIniciales.SoatTParVehiculoUsoFk);
                        AppDomain.CurrentDomain.SetData("SoatTParVehiculoTipoFk", datosIniciales.SoatTParVehiculoTipoFk);
                        AppDomain.CurrentDomain.SetData("SoatTParDepartamentoPcFk", datosIniciales.SoatTParDepartamentoPcFk);
                          */
                        //resMensaje = msgIso8583.Build(messageRespuesta, MTI);
                    }
                }
                else //no es vendible
                {
                    string[] messageRespuesta = new string[130];
                    MTI = "0110";
                    messageRespuesta[3] = "020000";
                    messageRespuesta[39] = "01"; //CODIGO DE RESPUESTA,00=ENROLADO Y SI VENDIBLE,10= NO ENROLADO PERO SI VENDIBLE , 01 NO VENDIBLE
                    messageRespuesta[62] = mensajeError;
                    string nonASCIIsection = "";
                    resMensaje = msgIso8583.BuildCustomISO(messageRespuesta, MTI, out nonASCIIsection);

                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error metodo Ven01ValidarVendibleYObtenerDatos,detalle:" + ex.Message + ex.InnerException + ex.StackTrace);
                string[] messageRespuesta = new string[130];
                MTI = "0110";
                messageRespuesta[3] = "020000";
                messageRespuesta[39] = "99"; //CODIGO DE RESPUESTA,00=ENROLADO Y SI VENDIBLE,10= NO ENROLADO PERO SI VENDIBLE , 01 NO VENDIBLE, 99 ERROR
                messageRespuesta[62] = mensajeError;
                resMensaje = msgIso8583.Build(messageRespuesta, MTI);
            }
            Logger.Info("mensaje enviado:" + resMensaje);
            return resMensaje;
        }
        string Autenticacion(string usuario, string password, string ip)
        {
            //autenticar en BD
            // usuario = "ESEGOBIANO";
            // password = "123456";
            string resMensaje = "";
            ISO8583 msgIso8583 = new ISO8583();
            int token = 0;
            //llamada al metodo de seguridad
            IwsSeguridadClient client = new IwsSeguridadClient();
            CESeg01Autenticacion oCeSeg01Autenticacion = new CESeg01Autenticacion
            {
                Contrasenia = password,
                Ip = ip,
                SoatTIntermediarioFk = 0,
                Usuario = usuario
            };
            string nonAsciIsection = "";
            try
            {
                Logger.Info("llamada metodo Seg01Autenticacion," + JsonConvert.SerializeObject(oCeSeg01Autenticacion));
                CSSeg01Autenticacion res = client.Seg01Autenticacion(oCeSeg01Autenticacion);
                Logger.Info("Respuesta metodo Seg01Autenticacion" + JsonConvert.SerializeObject(res));
                if (res.Exito)
                {
                    token = res.SeguridadToken;
                    string outTransDatosUsuario = JsonConvert.SerializeObject(res.oTransUsuarioDatos, Formatting.Indented);
                    Logger.Info(outTransDatosUsuario);
                    IwsParametricasClient clientParametricasClient = new IwsParametricasClient();
                    CEParObtenerDepartamentos reqParObtenerDepartamentos = new CEParObtenerDepartamentos();
                    reqParObtenerDepartamentos.SeguridadToken = token;
                    reqParObtenerDepartamentos.SoatTIntermediarioFk = 0;
                    reqParObtenerDepartamentos.Usuario = usuario;
                    Logger.Info("llamada metodo ParObtenerDepartamentos");
                    CSParObtenerDepartamentos resParObtenerDepartamentos = clientParametricasClient.ParObtenerDepartamentos(reqParObtenerDepartamentos);
                    Logger.Info("Respuesta metodo ParObtenerDepartamentos" + resParObtenerDepartamentos.Mensaje);
                    if (resParObtenerDepartamentos.Exito)
                    {


                        string parametrosRes = "";
                        foreach (var depto in resParObtenerDepartamentos.lDepartamento)
                        {
                            parametrosRes += String.Format(@"D|{0}|{1}", depto.CodigoDepartamento, depto.Descripcion) + @"\";//+ (char)10;

                        }
                        CEParObtenerGestion reqCeParObtenerGestion = new CEParObtenerGestion();
                        reqCeParObtenerGestion.SeguridadToken = token;
                        reqCeParObtenerGestion.SoatTIntermediarioFk = 0;
                        reqCeParObtenerGestion.Usuario = usuario;
                        CSParObtenerGestion resCsParObtenerGestion = clientParametricasClient.ParObtenerGestion(reqCeParObtenerGestion);
                        Logger.Info("Respuesta metodo ParObtenerDepartamentos" + resCsParObtenerGestion.Mensaje);
                        if (resCsParObtenerGestion.Exito)
                        {
                            foreach (var gestion in resCsParObtenerGestion.lGestionLHabilitadas)
                            {
                                parametrosRes += String.Format(@"G|{0}|{1}", gestion.Secuencial, gestion.Secuencial) + @"\";
                            }
                            CEParObtenerVehiculoUsos reqCeParObtenerVehiculoUsos = new CEParObtenerVehiculoUsos();
                            reqCeParObtenerVehiculoUsos.SeguridadToken = token;
                            reqCeParObtenerVehiculoUsos.SoatTIntermediarioFk = 0;
                            reqCeParObtenerVehiculoUsos.Usuario = usuario;
                            Logger.Info("llamada metodo ParObtenerVehiculoUsos");
                            CSParObtenerVehiculoUsos resCsParObtenerVehiculoUsos =
                                clientParametricasClient.ParObtenerVehiculoUsos(reqCeParObtenerVehiculoUsos);
                            Logger.Info("Respuesta metodo ParObtenerVehiculoUsos" + resCsParObtenerVehiculoUsos.Mensaje);
                            if (resCsParObtenerVehiculoUsos.Exito)
                            {
                                foreach (var vehiUso in resCsParObtenerVehiculoUsos.lVehiculoUso)
                                {
                                    parametrosRes += String.Format(@"U|{0}|{1}", vehiUso.Secuencial, vehiUso.Descripcion) + @"\";//;
                                }
                                CEParObtenerVehiculoTipos reqCeParObtenerVehiculoTipos = new CEParObtenerVehiculoTipos();
                                reqCeParObtenerVehiculoTipos.SeguridadToken = token;
                                reqCeParObtenerVehiculoTipos.SoatTIntermediarioFk = 0;
                                reqCeParObtenerVehiculoTipos.Usuario = usuario;
                                Logger.Info("llamada metodo ParObtenerVehiculoTipos");
                                CSParObtenerVehiculoTipos resCsParObtenerVehiculoTipos =
                                    clientParametricasClient.ParObtenerVehiculoTipos(reqCeParObtenerVehiculoTipos);
                                Logger.Info("Respuesta metodo ParObtenerVehiculoTipos" + resCsParObtenerVehiculoTipos.Mensaje);
                                if (resCsParObtenerVehiculoTipos.Exito)
                                {
                                    foreach (var tipo in resCsParObtenerVehiculoTipos.lVehiculoTipo)
                                    {
                                        parametrosRes += String.Format(@"V|{0}|{1}", tipo.Secuencial, tipo.Descripcion) + @"\";//;
                                    }
                                    MTI = "0110";
                                    string[] messageRespuesta = new string[130]; ;
                                    messageRespuesta[3] = "010000";
                                    messageRespuesta[39] = "00";
                                    messageRespuesta[62] = token.ToString();
                                    messageRespuesta[63] = parametrosRes;
                                    resMensaje = msgIso8583.BuildCustomISO(messageRespuesta, MTI, out nonAsciIsection);
                                    List<SessionData> datosSesion = (List<SessionData>)AppDomain.CurrentDomain.GetData("SessionData");
                                    if (datosSesion == null)
                                    {
                                        datosSesion = new List<SessionData>();
                                    }
                                    if (datosSesion.FirstOrDefault(dt => dt.Token == token) != null) //en caso que exista el token se debe borrar 
                                    {
                                        datosSesion.Remove(datosSesion.FirstOrDefault(dt => dt.Token == token));

                                    }
                                    //añadimos datos nuevos al session data
                                    datosSesion.Add(new SessionData() { Token = token, Usuario = usuario, SucursalCodigo = res.oTransUsuarioDatos.SucursalCodigo });
                                    AppDomain.CurrentDomain.SetData("SessionData", datosSesion);


                                    

                                    //resMensaje = resMensaje.Substring(nonASCIIsection.Length + 1);

                                }
                            }

                        }
                        else
                        {
                            throw new Exception();

                        }
                    }
                    else
                    {
                        throw new Exception();
                    }
                }
                else
                {
                    throw new Exception();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error metodo Autenticacion,detalle:" + ex.Message + ex.InnerException + ex.StackTrace);
                MTI = "0110";
                string[] messageRespuesta = new string[130];
                messageRespuesta[3] = "010000";
                messageRespuesta[39] = "01"; //01 error 00 exito
                messageRespuesta[63] = ex.Message;
                resMensaje = msgIso8583.BuildCustomISO(messageRespuesta, MTI, out nonAsciIsection);

            }
            Logger.Info("mensaje enviado:" + resMensaje);
            return resMensaje;
        }
    }
}
