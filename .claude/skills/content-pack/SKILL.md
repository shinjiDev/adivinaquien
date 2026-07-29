---
name: content-pack
description: Diseñar, generar y validar mazos de contenido (content packs) para el juego tipo "Adivina Quién". Úsala SIEMPRE que se hable de cartas, atributos, preguntas, mazos, temáticas, o de agregar/modificar contenido del juego (bailes típicos, trajes típicos, lugares, animales, o cualquier otra temática), incluso si el usuario no menciona la palabra "content pack". También úsala al escribir el validador de mazos o al revisar si un mazo es jugable.
---

# Content Pack: diseño de mazos

Un **content pack** es la unidad que hace que el motor del juego sea agnóstico al tema.
El motor nunca sabe qué es un "baile" o un "traje": solo sabe de cartas con vectores de
atributos y preguntas que los particionan. Cambiar de tema = cambiar un JSON.

## 1. Esquema

```json
{
  "packId": "bailes-chile",
  "nombre": "Bailes típicos de Chile",
  "descripcion": "Adivina el baile folclórico chileno oculto del oponente.",
  "idioma": "es-CL",
  "version": "1.0.0",
  "atributos": [
    {
      "id": "zona",
      "tipo": "categorico",
      "etiqueta": "Zona",
      "valores": [
        { "id": "norte",    "etiqueta": "Norte",    "pregunta": "¿Es un baile de la zona norte?" },
        { "id": "centro",   "etiqueta": "Centro",   "pregunta": "¿Es un baile de la zona central?" },
        { "id": "sur",      "etiqueta": "Sur",      "pregunta": "¿Es un baile de la zona sur?" },
        { "id": "austral",  "etiqueta": "Austral",  "pregunta": "¿Es un baile de la zona austral?" },
        { "id": "insular",  "etiqueta": "Insular",  "pregunta": "¿Es un baile de Rapa Nui?" }
      ]
    },
    {
      "id": "usa_panuelo",
      "tipo": "booleano",
      "etiqueta": "Pañuelo",
      "pregunta": "¿Se baila con pañuelo?"
    },
    {
      "id": "instrumentos",
      "tipo": "multivalor",
      "etiqueta": "Instrumentos",
      "valores": [
        { "id": "guitarra",  "etiqueta": "Guitarra",  "pregunta": "¿Se acompaña con guitarra?" },
        { "id": "acordeon",  "etiqueta": "Acordeón",  "pregunta": "¿Se acompaña con acordeón?" },
        { "id": "percusion", "etiqueta": "Percusión", "pregunta": "¿Se acompaña con percusión?" }
      ]
    },
    {
      "id": "n_bailarines",
      "tipo": "ordinal",
      "etiqueta": "Formación",
      "valores": [
        { "id": "individual", "orden": 1, "etiqueta": "Individual", "pregunta": "¿Se baila solo?" },
        { "id": "pareja",     "orden": 2, "etiqueta": "En pareja",  "pregunta": "¿Se baila en pareja?" },
        { "id": "grupo",      "orden": 3, "etiqueta": "En grupo",   "pregunta": "¿Se baila en grupo?" }
      ]
    }
  ],
  "cartas": [
    {
      "id": "cueca",
      "nombre": "Cueca",
      "imagen": "img/cueca.webp",
      "atributos": {
        "zona": "centro",
        "usa_panuelo": true,
        "instrumentos": ["guitarra", "acordeon"],
        "n_bailarines": "pareja"
      },
      "ficha": "Texto breve que se muestra al revelar la carta.",
      "fuente": "URL o referencia bibliográfica verificable"
    }
  ]
}
```

**Tipos de atributo soportados:** `booleano`, `categorico` (un valor), `multivalor`
(cero o más valores), `ordinal` (categórico con orden, permite preguntas de umbral).

**Qué hacen los atributos, y qué no.** El juego permite **preguntas en texto libre**: el
jugador escribe lo que quiera y el oponente responde mirando su carta. Los atributos no
restringen lo que se puede preguntar. Lo que hacen es tres cosas:

1. **Se imprimen en la carta** como etiquetas visibles, así que son de hecho *sobre lo
   que la gente pregunta*. Son el vocabulario del mazo.
2. **Garantizan que el mazo sea resoluble y equilibrado** (ver las reglas de §3). Un mazo
   que no las cumple no se arregla con preguntas libres: si dos cartas son idénticas,
   ninguna pregunta del mundo las distingue.
3. **Alimentan las preguntas sugeridas** que la UI ofrece como chips pulsables, para que
   nadie tenga que escribir desde cero en un teléfono.

Cada par `(atributo, valor)` genera una sugerencia. El "catálogo" del que habla el resto
de este documento es ese conjunto de sugerencias, no una lista cerrada de opciones.

## 2. Dimensionamiento: cuántos atributos y cuántas preguntas

Identificar una carta entre `N` requiere `log₂(N)` bits de información, y cada pregunta
de sí/no aporta como máximo 1 bit. Eso da el **mínimo teórico** de preguntas por partida.
Pero el catálogo de preguntas disponibles debe ser mucho mayor que ese mínimo, porque
la estrategia nace del excedente.

| Cartas | Bits (`log₂N`) | Ejes de atributo | Preguntas útiles en el catálogo | Preguntas por partida |
|---|---|---|---|---|
| 16 | 4.0 | 5–7 | 12–16 | 4–6 |
| **24** | **4.6** | **6–9** | **15–22** | **5–7** |
| 32 | 5.0 | 8–11 | 20–28 | 6–8 |

**Regla general:** `preguntas útiles ≈ 3× a 5× log₂(N)`, y `ejes ≈ log₂(N) + 2 a log₂(N) + 5`.

**Por qué el excedente.** Con 24 cartas y exactamente 5 booleanos perfectamente
balanceados e independientes, toda partida se resolvería en 5 preguntas siguiendo
siempre la misma línea óptima: matemáticamente perfecto y completamente aburrido. El
catálogo sobredimensionado es lo que crea caminos alternativos, decisiones reales y
espacio para equivocarse. No optimices el mazo hacia el mínimo.

**Por carta:** cada carta lleva un valor por eje (6–9 valores), y debería responder "sí"
a entre el 30% y el 50% del catálogo. Una carta que dice "sí" a 2 de 20 preguntas es
casi invisible: cuesta encontrarla y cuesta descartarla.

### Cómo un eje aporta preguntas

- Booleano → 1 pregunta
- Categórico de `k` valores → `k` preguntas (una por valor)
- Multivalor de `k` valores → `k` preguntas, y son las más independientes entre sí
- Ordinal de `k` valores → `k` preguntas, más preguntas de umbral si el motor las soporta

### Receta de composición para 24 cartas (≈8 ejes, ≈19 preguntas)

| Eje | Tipo | Valores | Preguntas |
|---|---|---|---|
| Geográfico / origen | categórico | 4–5 | 4–5 |
| Composición / estructura | categórico u ordinal | 3 | 3 |
| Elementos visibles | 4–5 booleanos | — | 4–5 |
| Rasgo múltiple (colores, materiales, instrumentos) | multivalor | 3–4 | 3–4 |
| Función / contexto | categórico | 3 | 3 |

Esta receta se traslada a cualquier dominio. Ejemplo con personajes de Disney (24 cartas,
8 ejes, 20 preguntas): `genero` (3) · `especie` humano/animal/objeto/criatura (4) ·
`rol` protagonista/villano/secundario (3) · `tiene_magia` (1) · `es_realeza` (1) ·
`epoca` clásica/renacimiento/moderna (3) · `canta` (1) · `color_dominante` multivalor (4).

### Test de 4 preguntas para aceptar un eje

Antes de agregar un eje al pack, debe pasar las cuatro:

1. **¿Es objetivo?** Dos personas informadas responden igual, sin discusión.
2. **¿Parte el mazo?** Cada uno de sus valores cae entre 15% y 85% de las cartas.
3. **¿Es independiente?** No está fuertemente correlacionado con un eje ya presente.
   *(En Disney, `es_realeza` y `rol=protagonista` suelen correlacionar: uno de los dos sobra.)*
4. **¿Se puede mostrar en la carta?** Si no cabe como etiqueta o ícono, no sirve (ver §5).

**Salida de emergencia.** Si un dominio no da para 15 preguntas útiles, hay dos opciones:
reducir el mazo a 16 cartas, o agregar un eje derivado del nombre —
`inicial` agrupada en A–F / G–M / N–Z siempre existe, siempre es objetiva y siempre parte
bien el mazo. Es un relleno legítimo, pero úsalo como último recurso: no enseña nada
sobre el dominio.

## 3. Reglas de validación (el validador debe implementarlas todas)

| # | Regla | Severidad |
|---|---|---|
| R1 | Tamaño del mazo entre 16 y 36 cartas. Recomendado: 24. | error fuera de rango |
| R2 | **Todos los vectores de atributos son únicos.** Dos cartas idénticas hacen la partida irresoluble. | error |
| R3 | Cada pregunta debe responder "sí" para entre 15% y 85% del mazo. Ideal: 35%–65%. | error <15% o >85%; aviso fuera de 25%–75% |
| R4 | Ninguna pareja de preguntas debe particionar el mazo de forma idéntica o casi idéntica (correlación ≥0.9). Son preguntas redundantes que desperdician el catálogo. | aviso |
| R5 | El árbol de decisión óptimo sobre las sugerencias debe resolver el mazo en ≤7 preguntas en el peor caso y ≤5 en promedio. Con preguntas libres los jugadores pueden hacerlo mejor, así que esto es una cota superior, no una predicción. | error si peor caso >8 |
| R6 | Cada carta debe poder eliminarse mediante al menos 3 preguntas distintas. Si no, es una carta "escondida" que alarga las partidas. | aviso |
| R7 | Mínimo 12 sugerencias utilizables (tras aplicar R3). Con menos, el mazo no tiene suficiente vocabulario visible para sostener una partida. | error |
| R8 | Todo atributo usado en una pregunta debe estar visible en la carta (ver §5). | error |

### Métrica: entropía por pregunta

Para una pregunta que responde "sí" en una fracción `p` del mazo:

```
H(p) = -p·log₂(p) - (1-p)·log₂(1-p)
```

`H = 1.0` bit es la partición perfecta (p = 0.5). Un mazo de 24 cartas necesita
`log₂(24) ≈ 4.58` bits. Con preguntas promediando 0.85 bits, eso son ~6 preguntas.
Descarta cualquier pregunta con `H < 0.61` (equivale a p fuera de 15%–85%).

El validador debe emitir un reporte con: distribución por pregunta, entropía por
pregunta, matriz de redundancia, profundidad del árbol óptimo, y lista de colisiones.

## 4. Proceso de diseño de un mazo nuevo

1. **Elegir 24 ítems** representativos y visualmente distinguibles del dominio.
2. **Elegir 6–9 ejes de atributos** usando la receta de composición de §2 y las
   plantillas de dominio de §6. Cada eje debe pasar el test de 4 preguntas (§2).
   Busca ejes *ortogonales*: si "zona=insular" y "raíz=rapanui" identifican exactamente
   las mismas cartas, uno de los dos sobra.
3. **Llenar la matriz** cartas × atributos. Este es el trabajo real; hazlo en una tabla
   antes de escribir el JSON.
4. **Correr el validador** y ajustar. Los ajustes típicos son: agregar un eje binario
   nuevo para romper colisiones, o reemplazar ítems demasiado parecidos entre sí.
5. **Verificar los hechos** con fuentes. Cada carta lleva su campo `fuente`.
6. **Producir las imágenes** (ver §6).

## 5. Verificabilidad: la adaptación clave

En el "Adivina Quién" original los atributos son *visibles* en el dibujo: barba, lentes,
sombrero. En un mazo cultural muchos atributos no lo son — "zona geográfica" o "raíz
mapuche" son datos, no dibujos.

**Solución obligatoria: cada carta muestra sus atributos como etiquetas o íconos en el
tablero.** Así nadie necesita saber folclore previamente para jugar, las respuestas son
objetivamente verificables, y la exposición repetida a los atributos es justamente el
valor educativo del juego.

Corolario: prohibidos los atributos subjetivos ("¿es alegre?", "¿es bonito?") salvo que
el pack los defina explícitamente y los muestre en la carta.

## 6. Plantillas de ejes por dominio

**Bailes típicos**
`zona` · `raiz_cultural` (indígena / criolla / mestiza / europea) · `formacion`
(individual / pareja / grupo) · `usa_panuelo` · `usa_mascara` · `zapateo` ·
`instrumento_principal` · `caracter` (cortejo / festivo / religioso / ceremonial /
satírico) · `calzado` · `fiesta_asociada`

**Trajes típicos**
`region` · `genero` · `prenda_superior` (manta / chamanto / poncho / rebozo / camisa) ·
`sombrero` (chupalla / paño / sin sombrero) · `calzado` (bota / ojota / descalzo) ·
`material` (lana / algodón / cuero) · `tecnica` (telar / bordado / tejido a palillo) ·
`color_dominante` · `uso` (diario / festivo / ceremonial) · `joyeria_metalica`

**Lugares típicos**
`region` · `tipo` (natural / urbano / patrimonial / religioso) · `geografia` (costa /
valle / cordillera / altiplano / archipiélago) · `clima` · `patrimonio_unesco` ·
`altitud` (ordinal) · `acceso` (carretera / barco / avión / caminata) ·
`actividad_principal` · `epoca_recomendada`

Nota que las tres comparten estructura: **un eje geográfico, uno de composición o
material, dos o tres booleanos de elementos visibles, y uno de función o contexto.**
Esa receta funciona para casi cualquier dominio.

## 7. Gramática española de las preguntas

**Nunca generes preguntas por concatenación.** `"¿Es " + valor + "?"` produce
concordancias rotas de género y número en español. Cada valor de atributo lleva su
plantilla `pregunta` escrita completa y revisada a mano en el JSON.

Si el pack necesita mostrar la respuesta negativa ("No, no se baila con pañuelo"),
agrega un campo opcional `preguntaNegada` en vez de derivarlo con reglas.

## 8. Contenido cultural: precauciones

- Usa los nombres en la lengua de origen cuando existan (mapudungun, aymara, rapa nui)
  y no los traduzcas ni los "castellanices".
- Distingue lo **ceremonial** de lo **festivo**. Algunas danzas y vestimentas tienen
  carácter ritual y no son folclore de espectáculo; márcalo en el atributo `caracter`
  y en la ficha.
- Cada carta debe llevar `fuente` verificable. Un modelo de lenguaje generando datos
  folclóricos **inventa detalles con facilidad**: el borrador lo hace Claude, la
  verificación la hace una persona antes de publicar.
- Ante duda entre precisión y jugabilidad, gana la precisión: ajusta el mazo, no el hecho.

## 9. Imágenes

- Formato cuadrado o 3:4, WebP, y **legibles a 120 px** — es el tamaño real en un
  tablero de 24 cartas en pantalla de teléfono.
- Estilo consistente en todo el mazo (misma técnica, mismo encuadre, mismo fondo).
- La silueta debe ser distinguible sin color, para accesibilidad.
- **Advertencia:** las imágenes generadas por IA de vestimenta tradicional suelen ser
  inexactas (mezclan elementos de culturas distintas). Prefiere fotografía con licencia
  libre e ilustración basada en referencias reales, con atribución en el pack.

## 10. Implementación (Fase 2)

El esquema vive en `AdivinaQue.Contracts/ContentPack/` (compartido con Server/Client a
futuro); el validador y el CLI viven en `AdivinaQue.PackTool/` (`Model/`, `Analysis/`,
`Validation/`, `Reporting/`). Uso: `dotnet run --project src/AdivinaQue.PackTool --
validate <ruta-a-pack.json>`.

Dos reglas de esta lista no tenían una definición operacional exacta en el spec; así
quedaron implementadas (`src/AdivinaQue.PackTool/Analysis/`):

- **R5 (árbol óptimo)**: heurística **greedy tipo ID3**, no búsqueda exhaustiva — en
  cada nodo elige la pregunta que minimiza el tamaño del grupo más grande resultante
  (criterio minimax). Un árbol verdaderamente óptimo es NP-duro en general; el spec no
  pide fuerza bruta.
- **R6 ("carta escondida")**: una pregunta "ayuda a eliminar" una carta si esa carta cae
  del lado **igual-o-más-chico** de la partición (mismo criterio minimax que R5). Cuenta,
  por carta, cuántas preguntas utilizables cumplen esto; aviso si son menos de 3.

R3 se evalúa sobre **todo** el catálogo crudo (para señalar qué preguntas están mal);
R4/R5/R6/R7 se evalúan sobre el catálogo ya filtrado por R3 (el "catálogo utilizable").
