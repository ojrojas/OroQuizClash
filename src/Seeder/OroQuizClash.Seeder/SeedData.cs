namespace OroQuizClash.Seeder;

internal sealed record CategorySeed(
    string Name,
    string Description,
    string KnowledgeArea,
    string AcademicLevel,
    int AgeMin,
    int AgeMax,
    int Difficulty,
    string[] Tags,
    string Color,
    string Icon
);

internal sealed record QuestionSeed(
    string Text,
    string[] Options, // 4
    int CorrectIndex, // 0-3
    int Difficulty,
    string AcademicLevel,
    int AgeMin,
    int AgeMax,
    int TimeSeconds,
    string Explanation
);

internal static class SeedData
{
    public static IReadOnlyList<CategorySeed> Categories { get; } =
    [
        new("Biología Celular", "Estructura y función de la célula, organelos y procesos celulares", "Ciencias", "Secundaria", 12, 17, 3, ["celula", "biologia", "secundaria"], "#16A34A", "microscope"),
        new("Genética y Herencia", "Leyes de Mendel, ADN, mutaciones y herencia humana", "Ciencias", "Secundaria", 13, 17, 3, ["genetica", "adn", "herencia"], "#7C3AED", "dna"),
        new("Ecología y Medio Ambiente", "Ecosistemas, biodiversidad, cambio climático y sostenibilidad", "Ciencias", "Secundaria", 12, 17, 2, ["ecologia", "medio-ambiente", "sostenibilidad"], "#059669", "leaf"),
        new("Anatomía Humana", "Sistemas del cuerpo humano, órganos y fisiología básica", "Ciencias", "Secundaria", 13, 17, 3, ["anatomia", "cuerpo-humano", "fisiologia"], "#DC2626", "heart-pulse"),
        new("Química Inorgánica", "Tabla periódica, enlaces, reacciones y estequiometría básica", "Ciencias", "Secundaria", 13, 17, 3, ["quimica", "inorganica", "tabla-periodica"], "#2563EB", "flask-conical"),
        new("Química Orgánica", "Hidrocarburos, grupos funcionales y reacciones orgánicas", "Ciencias", "Secundaria", 14, 17, 4, ["quimica", "organica", "hidrocarburos"], "#9333EA", "atom"),
        new("Física Mecánica", "Cinemática, dinámica, energía, trabajo y potencia", "Ciencias", "Secundaria", 13, 17, 3, ["fisica", "mecanica", "energia"], "#EA580C", "move"),
        new("Física Electricidad y Magnetismo", "Circuitos, corriente, campo eléctrico y magnético", "Ciencias", "Secundaria", 14, 17, 4, ["fisica", "electricidad", "magnetismo"], "#CA8A04", "zap"),
        new("Ciencias de la Tierra", "Geología, atmósfera, hidrosfera y fenómenos naturales", "Ciencias", "Secundaria", 12, 17, 2, ["geologia", "tierra", "atmosfera"], "#57534E", "mountain"),
        new("Astronomía", "Sistema solar, estrellas, galaxias y exploración espacial", "Ciencias", "Secundaria", 12, 17, 2, ["astronomia", "sistema-solar", "universo"], "#1E40AF", "star"),
    ];

    public static IReadOnlyDictionary<string, IReadOnlyList<QuestionSeed>> QuestionsByCategory { get; } = BuildQuestions();

    private static IReadOnlyDictionary<string, IReadOnlyList<QuestionSeed>> BuildQuestions()
    {
        var dict = new Dictionary<string, IReadOnlyList<QuestionSeed>>(StringComparer.OrdinalIgnoreCase);
        dict["Biología Celular"] = Create(
            ("¿Qué orgánulo se encarga de la respiración celular?", ["Mitocondria","Cloroplasto","Ribosoma","Núcleo"], 0, 2, "Mitocondria produce ATP mediante respiración celular."),
            ("¿Cuál es la función principal del retículo endoplasmático rugoso?", ["Síntesis de proteínas","Síntesis de lípidos","Almacenamiento de agua","Fotosíntesis"], 0, 2, ""),
            ("¿Qué estructura da rigidez a la célula vegetal?", ["Pared celular","Membrana plasmática","Citoplasma","Vacuola"], 0, 2, ""),
            ("¿Dónde ocurre la fotosíntesis?", ["Cloroplasto","Mitocondria","Núcleo","Lisosoma"], 0, 2, ""),
            ("¿Qué tipo de célula no tiene núcleo definido?", ["Procariota","Eucariota","Vegetal","Animal"], 0, 2, ""),
            ("¿Cuál es la unidad básica de la vida?", ["Célula","Tejido","Órgano","Sistema"], 0, 1, ""),
            ("¿Qué orgánulo contiene el material genético?", ["Núcleo","Mitocondria","Ribosoma","Aparato de Golgi"], 0, 1, ""),
            ("¿Qué proceso divide el citoplasma?", ["Citocinesis","Mitosis","Meiosis","Interfase"], 0, 3, ""),
            ("¿Cuál es la función de los lisosomas?", ["Digestión celular","Transporte","Fotosíntesis","Respiración"], 0, 3, ""),
            ("¿Qué molécula almacena la información genética?", ["ADN","ARN","Proteína","Lípido"], 0, 2, ""),
            ("¿Qué tipo de transporte requiere energía?", ["Activo","Pasivo","Difusión","Ósmosis"], 0, 3, ""),
            ("¿Cuál es la fase más larga del ciclo celular?", ["Interfase","Profase","Metafase","Telofase"], 0, 3, ""),
            ("¿Qué orgánulo sintetiza proteínas?", ["Ribosoma","Mitocondria","Cloroplasto","Vacuola"], 0, 2, ""),
            ("¿Qué estructura controla el paso de sustancias?", ["Membrana plasmática","Pared celular","Citoplasma","Núcleo"], 0, 2, ""),
            ("¿Qué es la ósmosis?", ["Movimiento de agua a través de membrana semipermeable","Movimiento de proteínas","División celular","Síntesis de ATP"], 0, 3, ""),
            ("¿Cuál es la función del aparato de Golgi?", ["Empaquetar y distribuir proteínas","Producir energía","Fotosíntesis","Almacenar agua"], 0, 3, ""),
            ("¿Qué células tienen cloroplastos?", ["Vegetales","Animales","Bacterias","Hongos"], 0, 2, ""),
            ("¿Qué es el citoplasma?", ["Sustancia gelatinosa donde flotan organelos","Núcleo","Pared celular","Membrana"], 0, 2, ""),
            ("¿Cuál es la diferencia entre mitosis y meiosis?", ["Meiosis produce 4 células haploides, mitosis 2 diploides","Son iguales","Mitosis produce 4","Meiosis produce 2"], 0, 4, ""),
            ("¿Qué es la membrana celular?", ["Bicapa lipídica que regula el intercambio","Pared rígida","Organelo","Núcleo"], 0, 2, "")
        );
        dict["Genética y Herencia"] = Create(
            ("¿Quién es el padre de la genética?", ["Gregor Mendel","Charles Darwin","Louis Pasteur","Alexander Fleming"], 0, 2, ""),
            ("¿Cuántos cromosomas tiene el ser humano?", ["46","44","48","42"], 0, 2, ""),
            ("¿Qué es un alelo?", ["Variante de un gen","Tipo de cromosoma","Proteína","Enzima"], 0, 2, ""),
            ("¿Qué representa el genotipo AA?", ["Homocigoto dominante","Heterocigoto","Homocigoto recesivo","Hemigoto"], 0, 3, ""),
            ("¿Qué es la mutación?", ["Cambio en la secuencia de ADN","División celular","Fotosíntesis","Respiración"], 0, 2, ""),
            ("¿Dónde se encuentra el ADN en eucariotas?", ["Núcleo","Mitocondria solo","Ribosoma","Citoplasma"], 0, 2, ""),
            ("¿Qué es el fenotipo?", ["Características observables","Genes","Cromosomas","Alelos"], 0, 2, ""),
            ("¿Cuál es la proporción mendeliana 3:1?", ["Monohíbrido Aa x Aa","Dihíbrido","Retrocruza","Codominancia"], 0, 3, ""),
            ("¿Qué es la herencia ligada al sexo?", ["Genes en cromosomas sexuales","Genes autosómicos","Mutación","Codominancia"], 0, 2, ""),
            ("¿Qué es el código genético?", ["Conjunto de reglas ADN→proteína","ADN","ARN","Proteína"], 0, 2, ""),
            ("¿Cuántas bases tiene el ADN?", ["4 (A,T,C,G)","2","6","8"], 0, 1, ""),
            ("¿Qué es un heterocigoto?", ["Aa","AA","aa","AAA"], 0, 1, ""),
            ("¿Qué es la transcripción?", ["ADN a ARN","ARN a proteína","ADN a ADN","Proteína a ADN"], 0, 2, ""),
            ("¿Qué es un cariotipo?", ["Foto ordenada de cromosomas","Gen","Alelo","Mutación"], 0, 2, ""),
            ("¿Qué determina el sexo en humanos?", ["Cromosomas X/Y","Autosomas","Mitocondria","Ribosoma"], 0, 2, ""),
            ("¿Qué es la dominancia incompleta?", ["Fenotipo intermedio","Dominante total","Recesivo","Codominancia"], 0, 2, ""),
            ("¿Qué es un gen?", ["Segmento de ADN que codifica proteína","Cromosoma","Alelo","Fenotipo"], 0, 1, ""),
            ("¿Qué es la replicación semiconservativa?", ["Cada hebra sirve de molde","Conserva una hebra","No conserva","Aleatoria"], 0, 1, ""),
            ("¿Qué es un organismo transgénico?", ["Con genes de otra especie","Mutante natural","Híbrido","Clon"], 0, 1, ""),
            ("¿Qué es la epigenética?", ["Cambios heredables sin alterar ADN","Mutación","Transcripción","Traducción"], 0, 3, "")
        );
        dict["Ecología y Medio Ambiente"] = Create(
            ("¿Qué es un ecosistema?", ["Comunidad + ambiente abiótico","Solo animales","Solo plantas","Solo agua"], 0, 1, ""),
            ("¿Qué es la biodiversidad?", ["Variedad de seres vivos","Un solo ecosistema","Solo genes","Solo especies"], 0, 1, ""),
            ("¿Qué gas aumenta el efecto invernadero?", ["CO2","Oxígeno","Nitrógeno","Helio"], 0, 1, ""),
            ("¿Qué es la cadena trófica?", ["Flujo de energía entre niveles","Cadena de ADN","Ciclo del agua","Fotosíntesis"], 0, 1, ""),
            ("¿Qué son los descomponedores?", ["Bacterias y hongos","Plantas","Animales","Algas"], 0, 1, ""),
            ("¿Qué es la deforestación?", ["Tala masiva de bosques","Plantar árboles","Reciclar","Compostar"], 0, 1, ""),
            ("¿Qué energía es renovable?", ["Solar","Carbón","Petróleo","Gas natural"], 0, 1, ""),
            ("¿Qué es el pH neutro?", ["7","0","14","1"], 0, 2, ""),
            ("¿Qué es el ciclo del agua?", ["Evaporación-condensación-precipitación","Fotosíntesis","Respiración","Digestión"], 0, 1, ""),
            ("¿Qué causa la lluvia ácida?", ["SO2 y NOx","O2","CO","H2"], 0, 1, ""),
            ("¿Qué es un bioma?", ["Gran comunidad con clima similar","Una especie","Un gen","Un ecosistema pequeño"], 0, 1, ""),
            ("¿Qué es la sucesión ecológica?", ["Cambio gradual de comunidades","Extinción","Migración","Deforestación"], 0, 2, ""),
            ("¿Qué es la huella ecológica?", ["Impacto humano en recursos","Huella de animal","Tipo de suelo","Clima"], 0, 1, ""),
            ("¿Qué protege la capa de ozono?", ["Radiación UV","Calor","Frío","Lluvia"], 0, 1, ""),
            ("¿Qué es el compostaje?", ["Degradación de materia orgánica","Quemar basura","Reciclar plástico","Verter al mar"], 0, 1, ""),
            ("¿Qué es un recurso no renovable?", ["Petróleo","Solar","Eólica","Hidráulica"], 0, 1, ""),
            ("¿Qué es la sostenibilidad?", ["Uso sin agotar recursos futuros","Uso máximo","No usar recursos","Solo reciclar"], 0, 1, ""),
            ("¿Qué es la eutrofización?", ["Exceso de nutrientes en agua","Falta de agua","Exceso de oxígeno","Falta de luz"], 0, 1, ""),
            ("¿Qué son las energías fósiles?", ["Restos orgánicos antiguos","Sol y viento","Agua","Geotermia"], 0, 1, ""),
            ("¿Qué es un corredor biológico?", ["Conexión entre hábitats","Carretera","Río","Montaña"], 0, 1, "")
        );
        dict["Anatomía Humana"] = Create(
            ("¿Cuántos huesos tiene el adulto?", ["206","180","250","300"], 0, 2, ""),
            ("¿Qué órgano bombea la sangre?", ["Corazón","Pulmón","Hígado","Riñón"], 0, 1, ""),
            ("¿Dónde ocurre el intercambio gaseoso?", ["Alvéolos","Tráquea","Bronquios","Faringe"], 0, 1, ""),
            ("¿Qué sistema controla con hormonas?", ["Endocrino","Nervioso","Digestivo","Respiratorio"], 0, 1, ""),
            ("¿Cuántas cámaras tiene el corazón humano?", ["4","2","3","1"], 0, 2, ""),
            ("¿Qué células transportan oxígeno?", ["Eritrocitos","Leucocitos","Plaquetas","Neuronas"], 0, 1, ""),
            ("¿Qué órgano produce insulina?", ["Páncreas","Hígado","Riñón","Bazo"], 0, 2, ""),
            ("¿Cuál es el hueso más largo?", ["Fémur","Húmero","Tibia","Peroné"], 0, 1, ""),
            ("¿Qué parte del cerebro coordina equilibrio?", ["Cerebelo","Cerebro","Bulbo raquídeo","Hipotálamo"], 0, 2, ""),
            ("¿Qué músculo es involuntario?", ["Cardíaco","Bíceps","Tríceps","Cuádriceps"], 0, 1, ""),
            ("¿Dónde se absorben nutrientes?", ["Intestino delgado","Estómago","Intestino grueso","Boca"], 0, 1, ""),
            ("¿Qué filtra la sangre?", ["Riñón","Hígado","Pulmón","Corazón"], 0, 1, ""),
            ("¿Cuántos pares craneales hay?", ["12","10","14","8"], 0, 1, ""),
            ("¿Qué es la homeóstasis?", ["Equilibrio interno","Enfermedad","Crecimiento","Reproducción"], 0, 1, ""),
            ("¿Qué produce la médula ósea?", ["Células sanguíneas","Orina","Bilis","Insulina"], 0, 1, ""),
            ("¿Qué une músculo con hueso?", ["Tendón","Ligamento","Cartílago","Fascia"], 0, 1, ""),
            ("¿Qué sistema defiende de patógenos?", ["Inmune","Digestivo","Respiratorio","Endocrino"], 0, 1, ""),
            ("¿Dónde se produce la voz?", ["Laringe","Faringe","Tráquea","Bronquios"], 0, 1, ""),
            ("¿Qué es el diafragma?", ["Músculo respiratorio","Hueso","Órgano digestivo","Vaso"], 0, 1, ""),
            ("¿Cuál es la articulación más móvil?", ["Hombro","Rodilla","Codo","Tobillo"], 0, 1, "")
        );
        dict["Química Inorgánica"] = Create(
            ("¿Símbolo del sodio?", ["Na","So","Sd","N"], 0, 1, ""),
            ("¿Qué es un ion?", ["Átomo con carga","Átomo neutro","Molécula","Isótopo"], 0, 1, ""),
            ("¿pH de un ácido fuerte?", ["<7","7",">7","0-14 cualquiera"], 0, 1, ""),
            ("¿Qué es la valencia?", ["Capacidad de combinación","Masa","Carga","Tamaño"], 0, 1, ""),
            ("¿Fórmula del agua?", ["H2O","HO2","H2O2","OH"], 0, 1, ""),
            ("¿Qué mide la molaridad?", ["Moles por litro","Gramos por litro","Moles por kilo","Presión"], 0, 1, ""),
            ("¿Qué es un óxido?", ["Compuesto con oxígeno","Con hidrógeno","Con nitrógeno","Con carbono"], 0, 1, ""),
            ("¿Qué gas forma el ozono?", ["O3","O2","O","O4"], 0, 1, ""),
            ("¿Qué es la electronegatividad?", ["Tendencia a atraer electrones","Carga","Masa","Radio"], 0, 1, ""),
            ("¿Cuál es el ácido del vinagre?", ["Acético","Sulfúrico","Clorhídrico","Nítrico"], 0, 1, ""),
            ("¿Qué es un precipitado?", ["Sólido insoluble formado","Gas","Líquido","Ion"], 0, 1, ""),
            ("¿Ley de conservación de masa?", ["Masa no se crea ni destruye","Masa se crea","Masa se destruye","Masa es energía"], 0, 1, ""),
            ("¿Qué es un isótopo?", ["Mismo elemento, distinto neutrón","Distinto elemento","Mismo neutrón","Distinto protón"], 0, 1, ""),
            ("¿Fórmula de la sal común?", ["NaCl","KCl","CaCl","MgCl"], 0, 1, ""),
            ("¿Qué es un enlace iónico?", ["Transferencia de electrones","Compartir electrones","Fuerza débil","Puente de hidrógeno"], 0, 1, ""),
            ("¿Qué indica el número atómico?", ["Protones","Neutrones","Electrones","Masa"], 0, 1, ""),
            ("¿Qué es la estequiometría?", ["Cálculo de proporciones en reacción","Medir pH","Medir temperatura","Medir volumen"], 0, 1, ""),
            ("¿Cuál es la base fuerte común?", ["NaOH","HCl","H2SO4","CH3COOH"], 0, 1, ""),
            ("¿Qué es un catalizador?", ["Acelera reacción sin consumirse","Se consume","Frena reacción","Es producto"], 0, 1, ""),
            ("¿Qué es una disolución?", ["Mezcla homogénea","Heterogénea","Sólido puro","Gas puro"], 0, 1, "")
        );
        dict["Química Orgánica"] = Create(
            ("¿Qué caracteriza un alcano?", ["Enlaces simples","Doble enlace","Triple enlace","Anillo aromático"], 0, 2, ""),
            ("¿Fórmula del metano?", ["CH4","C2H6","C3H8","C4H10"], 0, 1, ""),
            ("¿Grupo funcional del alcohol?", ["-OH","-COOH","-CHO","-NH2"], 0, 1, ""),
            ("¿Qué es un isómero?", ["Misma fórmula, distinta estructura","Distinta fórmula","Mismo nombre","Distinto peso"], 0, 1, ""),
            ("¿Qué es el benceno?", ["Anillo aromático C6H6","Alcano","Alqueno","Alquino"], 0, 2, ""),
            ("¿Grupo del ácido carboxílico?", ["-COOH","-OH","-CHO","-CO-"], 0, 1, ""),
            ("¿Qué es un polímero?", ["Macromolécula de monómeros","Monómero","Átomo","Ion"], 0, 1, ""),
            ("¿Qué es la hibridación sp3?", ["Tetraédrica 109.5°","Lineal 180°","Trigonal 120°","Plana"], 0, 2, ""),
            ("¿Qué es un éster?", ["R-COO-R'","R-OH","R-COOH","R-NH2"], 0, 1, ""),
            ("¿Reacción de sustitución en alcanos?", ["Halogenación","Adición","Eliminación","Polimerización"], 0, 1, ""),
            ("¿Qué es un alqueno?", ["Doble enlace C=C","Simple","Triple","Aromático"], 0, 1, ""),
            ("¿Qué es un alquino?", ["Triple enlace C≡C","Simple","Doble","Aromático"], 0, 1, ""),
            ("¿Grupo de la cetona?", ["-CO-","-OH","-COOH","-CHO"], 0, 1, ""),
            ("¿Qué es la isomería geométrica?", ["Cis-trans por rigidez","Cadena","Posición","Función"], 0, 2, ""),
            ("¿Qué es el etanol?", ["CH3CH2OH","CH3OH","C2H6","CH4"], 0, 1, ""),
            ("¿Qué es un aminoácido?", ["Con -NH2 y -COOH","Solo -OH","Solo -COOH","Solo -NH2"], 0, 1, ""),
            ("¿Qué es la polimerización por adición?", ["Monómeros se unen sin perder átomos","Con pérdida de agua","Con pérdida de HCl","Por condensación"], 0, 1, ""),
            ("¿Qué es el petróleo?", ["Mezcla de hidrocarburos fósiles","Alcohol","Ácido","Base"], 0, 1, ""),
            ("¿Qué es un hidrocarburo aromático?", ["Con anillo bencénico","Alcano","Alqueno","Alquino"], 0, 1, ""),
            ("¿Qué es la nomenclatura IUPAC?", ["Sistema oficial de nombres","Común","Trivial","Histórico"], 0, 1, "")
        );
        dict["Física Mecánica"] = Create(
            ("¿Fórmula de velocidad media?", ["v=d/t","v=t/d","v=d*t","v=d+t"], 0, 1, ""),
            ("¿Unidad de fuerza en SI?", ["Newton","Joule","Watt","Pascal"], 0, 1, ""),
            ("¿Ley de inercia?", ["1ª de Newton","2ª de Newton","3ª de Newton","Hooke"], 0, 1, ""),
            ("¿Qué es la aceleración?", ["Cambio de velocidad por tiempo","Cambio de posición","Fuerza","Energía"], 0, 1, ""),
            ("¿Energía cinética depende de?", ["masa y velocidad","solo masa","solo altura","solo tiempo"], 0, 1, ""),
            ("¿Trabajo = ?", ["F·d·cosθ","F/d","F·t","m·a"], 0, 1, ""),
            ("¿Potencia es?", ["Trabajo por tiempo","Fuerza por distancia","Masa por velocidad","Energía por masa"], 0, 1, ""),
            ("¿Qué es la inercia?", ["Resistencia a cambiar movimiento","Fuerza","Aceleración","Velocidad"], 0, 1, ""),
            ("¿3ª ley de Newton?", ["Acción-reacción","F=ma","Inercia","Gravitación"], 0, 1, ""),
            ("¿Energía potencial gravitatoria?", ["m·g·h","½mv²","F·d","m·a"], 0, 1, ""),
            ("¿Qué es el momento lineal?", ["m·v","m·a","F·t","½mv²"], 0, 1, ""),
            ("¿Conservación de energía?", ["Energía total constante sin pérdidas","Se crea","Se destruye","Depende de masa"], 0, 1, ""),
            ("¿Qué es el rozamiento?", ["Fuerza que opone al movimiento","Fuerza que impulsa","Energía","Potencia"], 0, 1, ""),
            ("¿Caída libre acelera con?", ["g≈9.8 m/s²","0","Depende de masa","Variable"], 0, 1, ""),
            ("¿Qué es un vector?", ["Magnitud y dirección","Solo magnitud","Solo dirección","Escalar"], 0, 1, ""),
            ("¿Fuerza centrípeta apunta a?", ["Centro de la trayectoria","Tangente","Exterior","Arriba"], 0, 1, ""),
            ("¿Qué es el impulso?", ["F·Δt","m·v","F·d","½mv²"], 0, 1, ""),
            ("¿Palanca de 1er género?", ["Fulcro en medio","Resistencia en medio","Potencia en medio","Sin fulcro"], 0, 1, ""),
            ("¿Qué es la presión?", ["Fuerza por área","Fuerza por volumen","Masa por volumen","Energía por tiempo"], 0, 1, ""),
            ("¿Principio de Arquímedes?", ["Empuje = peso fluido desalojado","F=ma","pV=nRT","E=mc²"], 0, 1, "")
        );
        dict["Física Electricidad y Magnetismo"] = Create(
            ("¿Ley de Ohm?", ["V=I·R","P=V·I","F=qE","B=μI/2πr"], 0, 1, ""),
            ("¿Unidad de carga?", ["Coulomb","Ampere","Volt","Ohm"], 0, 1, ""),
            ("¿Qué es un circuito en serie?", ["Misma corriente","Mismo voltaje","Corriente dividida","Resistencia 0"], 0, 1, ""),
            ("¿Qué almacena un condensador?", ["Carga eléctrica","Corriente","Resistencia","Inductancia"], 0, 1, ""),
            ("¿Qué crea un imán?", ["Campo magnético","Campo eléctrico solo","Gravedad","Luz"], 0, 1, ""),
            ("¿Ley de Coulomb?", ["F=k·q1q2/r²","V=IR","P=IV","F=ma"], 0, 1, ""),
            ("¿Qué es la corriente alterna?", ["Cambia de dirección periódicamente","Constante","Solo sube","Solo baja"], 0, 1, ""),
            ("¿Unidad de potencia eléctrica?", ["Watt","Joule","Coulomb","Tesla"], 0, 1, ""),
            ("¿Qué es un transformador?", ["Cambia voltaje AC","Cambia DC a AC","Almacena carga","Mide corriente"], 0, 1, ""),
            ("¿Qué es la inducción electromagnética?", ["Generar voltaje por campo variable","Calor por corriente","Luz por corriente","Sonido por campo"], 0, 1, ""),
            ("¿Qué es un semiconductor?", ["Conduce según condiciones","Conduce siempre","Aísla siempre","Es imán"], 0, 1, ""),
            ("¿Qué mide el voltímetro?", ["Diferencia de potencial","Corriente","Resistencia","Potencia"], 0, 1, ""),
            ("¿Qué es la resistencia?", ["Oposición al flujo de corriente","Facilita corriente","Almacena carga","Genera campo"], 0, 1, ""),
            ("¿Ley de Faraday?", ["ε=-dΦ/dt","V=IR","F=ma","E=mc²"], 0, 1, ""),
            ("¿Qué es un diodo?", ["Conduce en un sentido","Conduce ambos","Aísla ambos","Es resistencia"], 0, 1, ""),
            ("¿Qué es el campo eléctrico?", ["Región con fuerza sobre carga","Campo magnético","Gravedad","Luz"], 0, 1, ""),
            ("¿Unidad de campo magnético?", ["Tesla","Volt","Ampere","Ohm"], 0, 1, ""),
            ("¿Qué es un circuito en paralelo?", ["Mismo voltaje en ramas","Misma corriente","Una sola rama","Resistencia infinita"], 0, 1, ""),
            ("¿Qué es la ley de Kirchhoff de corrientes?", ["Suma corrientes en nodo =0","Suma voltajes =0","V=IR","P=IV"], 0, 1, ""),
            ("¿Qué hace un fusible?", ["Protege por fusión al exceso de corriente","Aumenta voltaje","Almacena energía","Mide corriente"], 0, 1, "")
        );
        dict["Ciencias de la Tierra"] = Create(
            ("¿Capas de la Tierra de fuera a dentro?", ["Corteza, manto, núcleo","Manto, corteza, núcleo","Núcleo, manto, corteza","Corteza, núcleo, manto"], 0, 1, ""),
            ("¿Qué causa los terremotos?", ["Movimiento de placas tectónicas","Viento","Lluvia","Mareas"], 0, 1, ""),
            ("¿Qué es la litosfera?", ["Capa rígida externa","Capa líquida","Núcleo","Atmósfera"], 0, 1, ""),
            ("¿Qué mide la escala Richter?", ["Magnitud sísmica","Temperatura","Presión","Humedad"], 0, 1, ""),
            ("¿Qué es un volcán?", ["Abertura que expulsa magma","Montaña sin magma","Río de lava frío","Grieta sin actividad"], 0, 1, ""),
            ("¿Ciclo de las rocas?", ["Ígnea-sedimentaria-metamórfica","Solo ígnea","Solo sedimentaria","No existe"], 0, 1, ""),
            ("¿Qué es la erosión?", ["Desgaste por agua/viento/hielo","Formación de rocas","Terremoto","Volcán"], 0, 1, ""),
            ("¿Capas de la atmósfera ordenadas?", ["Troposfera, estratosfera, mesosfera, termosfera","Estratosfera, troposfera...","Mesosfera primero","Termosfera primero"], 0, 1, ""),
            ("¿Qué causa las estaciones?", ["Inclinación del eje terrestre","Distancia al Sol","Rotación diaria","Mareas"], 0, 1, ""),
            ("¿Qué es un fósil?", ["Resto de organismo antiguo preservado","Roca ígnea","Mineral","Cristal"], 0, 1, ""),
            ("¿Qué es el efecto invernadero natural?", ["Retención de calor por gases","Enfriamiento","Sin efecto","Solo artificial"], 0, 1, ""),
            ("¿Qué es un acuífero?", ["Reserva subterránea de agua","Río","Lago","Océano"], 0, 1, ""),
            ("¿Qué es la deriva continental?", ["Movimiento de continentes","Erosión","Volcán","Terremoto puntual"], 0, 1, ""),
            ("¿Qué mide un barómetro?", ["Presión atmosférica","Temperatura","Humedad","Viento"], 0, 1, ""),
            ("¿Qué es un tsunami?", ["Ola gigante por sismo submarino","Marea normal","Ola de viento","Corriente"], 0, 1, ""),
            ("¿Qué es la meteorización?", ["Fragmentación de rocas in situ","Transporte","Deposición","Erosión lejana"], 0, 1, ""),
            ("¿Qué es el granito?", ["Roca ígnea intrusiva","Sedimentaria","Metamórfica","Volcánica extrusiva"], 0, 1, ""),
            ("¿Qué es la falla de San Andrés?", ["Límite transformante","Divergente","Convergente","No es falla"], 0, 1, ""),
            ("¿Qué es un glaciar?", ["Masa de hielo en movimiento","Nieve estacional","Río","Lago helado estático"], 0, 1, ""),
            ("¿Qué es la hidrosfera?", ["Conjunto de aguas del planeta","Solo océanos","Solo ríos","Solo hielo"], 0, 1, "")
        );
        dict["Astronomía"] = Create(
            ("¿Centro del sistema solar?", ["Sol","Tierra","Luna","Marte"], 0, 1, ""),
            ("¿Cuántos planetas tiene el sistema solar?", ["8","9","7","10"], 0, 1, ""),
            ("¿Qué es un año luz?", ["Distancia que recorre la luz en un año","Tiempo","Velocidad","Masa"], 0, 1, ""),
            ("¿Qué es una galaxia?", ["Conjunto de estrellas, gas y polvo","Un planeta","Una estrella","Un satélite"], 0, 1, ""),
            ("¿Cuál es la galaxia de la Vía Láctea?", ["Espiral barrada","Elíptica","Irregular","Lenticular"], 0, 1, ""),
            ("¿Qué es un agujero negro?", ["Región donde ni la luz escapa","Estrella brillante","Planeta oscuro","Nebulosa"], 0, 1, ""),
            ("¿Fases de la Luna por?", ["Posición relativa Sol-Tierra-Luna","Sombra de la Tierra siempre","Nubes","Rotación del Sol"], 0, 1, ""),
            ("¿Qué es un exoplaneta?", ["Planeta fuera del sistema solar","Luna","Asteroide","Cometa"], 0, 1, ""),
            ("¿Qué es la fotosfera?", ["Capa visible del Sol","Núcleo del Sol","Corona","Cromosfera"], 0, 1, ""),
            ("¿Cuál es la estrella más cercana?", ["Próxima Centauri","Sirio","Betelgeuse","Vega"], 0, 1, ""),
            ("¿Qué es un cometa?", ["Cuerpo helado con cola al acercarse al Sol","Asteroide rocoso","Planeta enano","Satélite"], 0, 1, ""),
            ("¿Qué causa los eclipses solares?", ["Luna entre Sol y Tierra","Tierra entre Sol y Luna","Nubes","Sombra de Marte"], 0, 1, ""),
            ("¿Qué es la ley de Hubble?", ["Universo en expansión","Gravedad","Relatividad","Cuántica"], 0, 1, ""),
            ("¿Qué es un púlsar?", ["Estrella de neutrones que pulsa","Agujero negro","Enana blanca","Gigante roja"], 0, 1, ""),
            ("¿Qué mide la magnitud estelar?", ["Brillo aparente","Tamaño","Masa","Temperatura solo"], 0, 1, ""),
            ("¿Qué es la materia oscura?", ["Materia no visible que gravita","Polvo","Gas caliente","Luz"], 0, 1, ""),
            ("¿Cuál es el planeta más grande?", ["Júpiter","Saturno","Tierra","Marte"], 0, 1, ""),
            ("¿Qué es un satélite natural?", ["Cuerpo que orbita un planeta","Planeta","Estrella","Galaxia"], 0, 1, ""),
            ("¿Qué es el Big Bang?", ["Origen expansivo del universo","Colisión de galaxias","Muerte estelar","Formación de la Luna"], 0, 1, ""),
            ("¿Qué es un telescopio espacial?", ["Observa sin distorsión atmosférica","Solo ve planetas","Es terrestre","Mide terremotos"], 0, 1, "")
        );
        return dict;
    }

    private static IReadOnlyList<QuestionSeed> Create(params (string text, string[] opts, int correct, int diff, string expl)[] items)
    {
        return items.Select(t => new QuestionSeed(t.text, t.opts, t.correct, t.diff, "Secundaria", 12, 17, 30, t.expl)).ToList();
    }
}
