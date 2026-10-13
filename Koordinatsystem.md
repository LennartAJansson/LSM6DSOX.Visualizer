# Primer: Geometri, 3D, Vektorer och IMU-data

*En lättförståelig guide för dig som vill förstå hur din pryl rör sig i rymden.*



---



## 1. Koordinatsystemet (X, Y, Z): Världen i 3D

För att en dator ska kunna förstå var saker finns eller hur de rör sig, delar den upp världen med tre osynliga linjer. Dessa linjer kallas för **axlar**, och tillsammans bildar de ett **koordinatsystem**.



Tänk dig att du sitter i ett flygplan:

*   **X-axeln (Bredd):** En linje som går rakt genom vingarna, från vänster till höger.

*   **Y-axeln (Höjd):** En linje som går rakt upp genom taket och ner genom golvet.

*   **Z-axeln (Djup):** En linje som går från flygplanets nos, genom kabinen och ut genom stjärten.



Där alla dessa tre linjer möts i mitten kallas för **origo** (nollpunkten). Varje rörelse din pryl gör mäts utifrån dessa tre linjer.



---



## 2. Vektorer: Pilarna som visar vägen

Ordet **vektor** låter som svår matematik, men i praktiken är det bara en **osynlig pil i rymden**. 



Om du kastar en pil i ett rum så har den alltid två egenskaper som datorn behöver veta:

1.  **Riktning:** Vart pekar pilspetsen? (Pekade du upp mot taket, eller framåt mot dörren?)

2.  **Magnitud (Längd):** Hur lång är pilen? I datorns värld betyder en längre pil att det finns mer kraft eller fart. En lång pil som pekar uppåt betyder alltså "hög fart uppåt".



I din applikation används vektorer för att tala om för datorn åt vilket håll prylen flyttar på sig, eller åt vilket håll tyngdkraften drar.






## 3. IMU-sensorn: Prylens tre "sinnen"

En **IMU** (*Inertial Measurement Unit*) är det lilla chipet på kretskortet som känner av rörelse. Chipet har inga ögon, så istället använder det tre olika inbyggda mätverktyg (sensorer) för att känna av världen:



### 🍏 Accelerometern (Känner av tyngdkraften)

Tänk dig att du blundar i en bil. När föraren gasar hårt trycks du bakåt i sätet. Det är acceleration. Accelerometern mäter exakt sådana krafter längs X-, Y- och Z-axeln. 

*   **Det smarta tricket:** Den känner också alltid av jordens dragningskraft (tyngdkraften). Även om din pryl ligger helt stilla på bordet, så känner accelerometern av en konstant kraft som drar "nedåt". 

*   **Vad appen har den till:** Att lista ut vad som är golv och tak (lutning).



### 🌀 Gyroskopet (Känner av snurrar)

Gyroskopet struntar i om du åker framåt eller bakåt. Det reagerar *bara* när prylen vrider eller roterar sig. Den mäter **vinkelhastighet** – det vill säga hur många grader prylen snurrar per sekund runt X-, Y- eller Z-axeln.

*   **Det smarta tricket:** Gyroskopet är blixtsnabbt. Om du gör en snabb kaströrelse med handen är det gyroskopet som hinner uppfatta det.

*   **Vad appen har den till:** Att fånga snabba, plötsliga rörelser i realtid.



### 🧭 Magnetometern (Kompassen)

Detta är en digital kompass. Den känner av jordens osynliga magnetfält.

*   **Vad appen har den till:** Den letar efter Nordpolen så att din pryl vet vilket väderstreck den pekar åt (om den är vänd mot norr, söder, öster eller väster).



---



## 4. Problemet: Sensorerna ljuger lite grann

Om datorn bara lyssnar på en sensor i taget blir det kaos i din applikation. De har nämligen olika svagheter:



*   **Gyroskopet lider av "Drift" (Glömska):** Gyroskopet mäter bara *hur fort* det snurrar just nu, inte vilken vinkel det har. Datorn måste därför plussa ihop alla svängar över tid. Det är som att gissa hur långt man har gått i ett mörkt rum. Efter en stund blir småfel till jättefel. Gyroskopet börjar "drifta" och tror att prylen snurrar, trots att den ligger helt stilla på bordet.

*   **Accelerometern lider av "Brus" (Darrighet):** Accelerometern är extremt känslig. Om din pryl skakar det minsta lilla (eller om en motor vibrerar i närheten) hoppar datan upp och ner som en galning. Datan blir full av "brus".



---



## 5. Lösningen: Madgwick-filtret (Den smarta chefen)

För att lösa detta använder du **Madgwick-filtret**. Det är en algoritm som håller på med något som kallas **sensorfusion** – det betyder helt enkelt att det slår ihop ("funderar ut") data från alla sensorer samtidigt för att hitta sanningen.



Madgwick-filtret fungerar som en smart chef:

> *"Gyroskopet påstår att vi har roterat 45 grader och fortfarande snurrar. Men accelerometern är helt stabil och säger att marken är exakt där den alltid har varit. Då förstår jag att gyroskopet har drabbat av 'drift'. Jag rensar bort felet!"*



Filtret tar det bästa från alla världar: gyroskopets snabbhet och accelerometerns/magnetometerns stabilitet.



---



## 6. Slutresultatet: Kvaternionen (Quaternion)

När Madgwick-filtret har räknat färdigt spottar det ur sig en **kvaternion** (på engelska *quaternion*). 



Människor gillar att tänka i vanliga vinklar: *Roll* (lutning höger/vänster), *Pitch* (lutning framåt/bakåt) och *Yaw* (svänga som en kompass). Men om datorn använder dessa vinklar drabbas den förr eller senare av **Gimbal Lock** (axellåsning). Det betyder att när prylen pekar rakt uppåt, så smälter två axlar ihop i datorns hjärna, och den blir plötsligt "blind" för om du svänger höger eller vänster.



En **kvaternion** löser detta genom att använda **fyra tal** `(W, X, Y, Z)` istället för tre vinklar:

1.  Den tänker ut en helt egen, osynlig **hjälp-axel** (som ett spett) rakt igenom föremålet i exakt rätt vinkel för just den rörelsen.

2.  Den talar om hur många **grader** föremålet ska rotera runt det där spettet.



Eftersom kvaternionen skapar en perfekt ny axel för varje unik position kan datorn aldrig hamna i en blind fläck eller drabbas av axellåsning. 



---



## Sammanfattning inför fikat

1.  **Koordinatsystemet:** De tre linjerna (X, Y, Z) som bygger upp 3D-rymden.

2.  **Vektor:** En osynlig pil som talar om åt vilket håll och med vilken kraft något rör sig.

3.  **IMU:** Chipet med de tre sinnena (Accelerometer för neråt, Gyroskop för svängar, Magnetometer för norr).

4.  **Madgwick-filtret:** Den smarta chefen som tvättar bort sensorernas darr och glömska.

5.  **Kvaternion:** Det färdiga paketet med fyra tal som ger din app en mjuk och helt felfri bild av hur prylen är vänd.



