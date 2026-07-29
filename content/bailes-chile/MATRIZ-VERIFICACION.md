# Bailes típicos de Chile — BORRADOR pendiente de verificación humana

Este mazo (`content/bailes-chile/pack.json`) fue generado por un modelo de lenguaje
siguiendo la skill `content-pack`. **No debe publicarse ni usarse en producción sin que
una persona revise los hechos folclóricos**, especialmente las filas marcadas con ⚠️.

## Estado del validador (`AdivinaQue.PackTool`)

```
RESULTADO: ACEPTADO — 0 errores, 5 avisos
Árbol de decisión: peor caso 6 preguntas, promedio 4,71
```

Avisos (no bloquean, quedan para tu criterio):

- `zona=norte` (21%) y `zona=insular` (17%) están un poco por debajo del ideal 25%-75%
  (aunque por encima del mínimo aceptable de 15%). Reflejan honestamente cuántos bailes
  norteños/rapanui reales y bien documentados encontré para 24 cartas — no rellené con
  cartas inventadas solo para emparejar el porcentaje.
- `caracter=cortejo` (21%) y `caracter=ceremonial` (21%) — mismo caso.
- `zona=centro` y `raiz_cultural=criolla` están perfectamente correlacionadas (phi=1.00):
  las 9 cartas de la zona central que elegí son, todas, de tradición criolla. Si conoces
  un baile típico de la zona central de raíz indígena o mestiza bien documentado, agregarlo
  rompería esta redundancia — no quise forzar una clasificación dudosa solo para variar.

## Decisiones de diseño que te pido confirmar explícitamente

1. **Eliminé "austral" (Magallanes/Aysén) como valor de `zona`.** El pack de ejemplo de
   la skill lo incluye, pero no encontré bailes típicos australes distintos, nombrados y
   verificables sin inventar contenido — el folclore de esa zona está mucho menos
   catalogado como "danza típica" separada de la tradición central/chilota. Preferí
   omitirlo a arriesgar fabricar nombres.
2. **Eliminé `usa_mascara` como atributo.** Casi ningún baile de los 24 que elegí usa
   máscara de forma bien documentada (el uso de máscaras en Chile se asocia sobre todo a
   fiestas religiosas tipo diablada, que no están en este mazo) — un atributo donde casi
   todas las cartas responden "no" no pasa la regla R3, así que lo saqué en vez de forzar
   casos dudosos a "sí".
3. **`formacion` (individual/pareja/grupo) se simplificó a `es_grupal` (booleano).**
   Encontré muy pocos bailes chilenos genuinamente solistas — mantener "individual" como
   tercer valor habría necesitado inventar población para esa categoría.
4. **Eliminé `caracter=satirico`** (solo "Pequén" y "El Pavo" lo justificaban con
   confianza — 2 de 24, bajo el mínimo de R3) y los reclasifiqué como `festivo`,
   mencionando el tono humorístico en la ficha en vez de en un atributo dedicado.
5. **Las 4 cartas de Rapa Nui (Sau-Sau, Tamure, Hoko, Ature) son las de menor confianza
   de todo el mazo.** Tamure y Sau-Sau tienen orígenes documentados fuera de Rapa Nui
   (tahitiano/polinésico general) adoptados en su repertorio folclórico actual; Ature
   podría clasificarse más como canto ritual que como baile. Revísalas con especial
   cuidado o considera reemplazarlas.

## Matriz cartas × atributos

⚠️ = la carta tiene uno o más atributos marcados `_verificar` en el JSON (ver columna final).

| Carta | Zona | Raíz cultural | Grupal | Pañuelo | Zapateo | Instrumentos | Carácter | ⚠️ Verificar |
|---|---|---|---|---|---|---|---|---|
| Cueca nortina | Norte | Mestiza | No | Sí | Sí | Percusión, Viento | Festivo | ⚠️ instrumentos |
| Trote | Norte | Indígena | No | No | No | Viento, Percusión, Acordeón | Festivo | ⚠️ raíz cultural |
| Huayño | Norte | Indígena | No | No | No | Viento, Percusión, Acordeón | Festivo | ⚠️ instrumentos |
| Cachimbo | Norte | Mestiza | No | Sí | Sí | Guitarra, Percusión | Cortejo | ⚠️ carácter |
| Cacharpaya | Norte | Indígena | Sí | No | No | Viento, Percusión | Festivo | — |
| Cueca | Centro | Criolla | No | Sí | Sí | Guitarra, Percusión | Cortejo | ⚠️ fuente (decreto) |
| Refalosa | Centro | Criolla | No | Sí | No | Guitarra | Cortejo | — |
| Sajuriana | Centro | Criolla | No | Sí | Sí | Guitarra, Percusión | Festivo | ⚠️ ficha |
| Pequén | Centro | Criolla | No | No | Sí | Guitarra, Percusión | Festivo | — |
| Cuando | Centro | Criolla | Sí | No | No | Guitarra | Ceremonial | ⚠️ ficha |
| Porteñada | Centro | Criolla | No | Sí | Sí | Guitarra, Acordeón | Festivo | ⚠️ **nombre completo, todo** (confianza baja) |
| Costillar | Centro | Criolla | No | No | Sí | Guitarra | Festivo | ⚠️ **nombre completo, todo** (confianza baja) |
| Rin | Centro | Criolla | No | No | No | Guitarra, Acordeón | Cortejo | ⚠️ atributos, ficha, fuente |
| El Chocolate | Centro | Criolla | No | No | No | Guitarra | Festivo | ⚠️ **nombre completo, todo** (confianza baja) |
| Cueca chilota | Sur | Mestiza | No | Sí | Sí | Acordeón, Percusión | Cortejo | — |
| Pericona | Sur | Mestiza | Sí | No | No | Acordeón, Percusión | Festivo | — |
| Sirilla | Sur | Mestiza | No | No | No | Acordeón | Festivo | ⚠️ raíz cultural |
| El Pavo (chilote) | Sur | Mestiza | No | No | No | Acordeón, Percusión | Festivo | ⚠️ **nombre completo, todo** (confianza baja) |
| Choique Purrún | Sur | Indígena | Sí | No | No | Viento, Percusión | Ceremonial | — |
| Loncomeo | Sur | Indígena | No | No | No | Percusión, Viento | Ceremonial | ⚠️ ¿es grupal? |
| Sau-Sau | Insular | Mestiza | Sí | No | No | Guitarra, Percusión | Festivo | ⚠️ **raíz, instrumentos, todo** (confianza baja) |
| Tamure | Insular | Mestiza | No | No | No | Percusión, Guitarra | Festivo | ⚠️ **raíz, todo** (confianza baja — origen tahitiano) |
| Hoko | Insular | Indígena | Sí | No | Sí | Percusión | Ceremonial | ⚠️ **todo** (confianza baja) |
| Ature | Insular | Indígena | Sí | No | No | Percusión | Ceremonial | ⚠️ **todo — ¿es canto, no baile?** |

## Qué falta para pasar de borrador a publicable

1. Una persona con conocimiento de folclore chileno (o fuentes académicas: Memoria
   Chilena/DIBAM, Sociedad Chilena del Folklore, etnomusicología mapuche y rapanui)
   revisa cada fila ⚠️ y corrige nombres, atributos o fichas donde corresponda.
   Especialmente: **Porteñada, Costillar, El Chocolate, El Pavo** (existencia y detalles
   de baja confianza) y las **4 cartas de Rapa Nui** (clasificación cultural).
2. Decidir si se reincorpora `austral` como zona (con contenido real) o se deja fuera.
3. Reemplazar los placeholders `img/<id>.webp` por las imágenes reales (formato
   cuadrado/3:4, WebP, legible a 120px — ver skill `content-pack` §9).
4. Confirmar/reemplazar los campos `fuente` genéricos por citas verificables concretas.
5. Volver a correr `dotnet run --project src/AdivinaQue.PackTool -- validate
   content/bailes-chile/pack.json` después de cualquier edición de contenido, por si
   una corrección cambia algún porcentaje o vector.
