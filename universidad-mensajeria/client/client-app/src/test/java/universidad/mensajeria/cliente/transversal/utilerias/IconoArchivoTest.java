package universidad.mensajeria.cliente.transversal.utilerias;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.List;
import java.util.Map;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;

import javafx.application.Platform;
import javafx.scene.image.Image;

import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.Test;

/**
 * Catálogo de iconos por tipo de archivo: la extensión decide el PNG y el PNG
 * tiene que estar de verdad en el classpath. Un catálogo que apunte a un recurso
 * que no se empaquetó devolvería {@code null} y nadie se enteraría hasta ver el
 * 📎 de siempre en la burbuja.
 */
class IconoArchivoTest {

    @BeforeAll
    static void iniciarJavaFx() throws InterruptedException {
        CountDownLatch iniciado = new CountDownLatch(1);
        try {
            Platform.startup(iniciado::countDown);
            if (!iniciado.await(20, TimeUnit.SECONDS)) {
                throw new IllegalStateException("JavaFX no inició dentro del tiempo esperado.");
            }
        } catch (IllegalStateException yaIniciado) {
            if (!"Toolkit already initialized".equals(yaIniciado.getMessage())) {
                throw yaIniciado;
            }
        }
    }

    @Test
    void cadaExtensionDelCatalogoApuntaASuIcono() {
        assertEquals("pdf", IconoArchivo.clave("informe.pdf"));
        assertEquals("pdf", IconoArchivo.clave("INFORME.PDF"), "la extensión no distingue mayúsculas");
        assertEquals("pdf", IconoArchivo.clave("C:/descargas/acta final.pdf"));
        assertEquals("xlsx", IconoArchivo.clave("notas.xlsx"));
        assertEquals("xlsx", IconoArchivo.clave("notas.xls"));
        assertEquals("csv", IconoArchivo.clave("alumnos.csv"));
        assertEquals("json", IconoArchivo.clave("config.json"));
        assertEquals("docs", IconoArchivo.clave("tesis.docx"));
        assertEquals("docs", IconoArchivo.clave("tesis.doc"));
        assertEquals("docs", IconoArchivo.clave("apuntes.odt"));
        assertEquals("docs", IconoArchivo.clave("notas.txt"));
        assertEquals("docs", IconoArchivo.clave("LEEME.md"));
    }

    @Test
    void loQueNoTieneIconoSeQuedaConElGenericoDeSiempre() {
        // Sin icono no se inventa uno: quien pinta sigue poniendo el 📎.
        assertNull(IconoArchivo.clave("diapositivas.pptx"));
        assertNull(IconoArchivo.clave("paquete.zip"));
        assertNull(IconoArchivo.clave("video.mp4"));
        assertNull(IconoArchivo.clave("sin-extension"));
        assertNull(IconoArchivo.clave("termina.en.punto."));
        assertNull(IconoArchivo.clave(null));
        assertNull(IconoArchivo.imagen("paquete.zip", IconoArchivo.ANCHO_BURBUJA));
    }

    @Test
    void losCincoIconosDelCatalogoExistenYSeDecodifican() {
        // Un nombre de archivo REAL por cada icono: la clave del catálogo
        // ("docs", "xlsx"...) no es una extensión y usarla como tal no probaría
        // nada. Lo importante es que el PNG esté empaquetado y se decodifique.
        var ejemplos = List.of(
                Map.entry("informe.pdf", "pdf"),
                Map.entry("notas.xlsx", "xlsx"),
                Map.entry("alumnos.csv", "csv"),
                Map.entry("config.json", "json"),
                Map.entry("tesis.docx", "docs"));
        for (var ejemplo : ejemplos) {
            String clave = ejemplo.getValue();
            String ruta = IconoArchivo.recurso(ejemplo.getKey());
            assertEquals("/iconos/" + clave + ".png", ruta,
                    "el recurso de " + clave + " no está en el classpath");
            assertNotNull(IconoArchivo.class.getResource(ruta));
            Image imagen = IconoArchivo.imagen(ejemplo.getKey(), IconoArchivo.ANCHO_BURBUJA);
            assertNotNull(imagen, "no se pudo decodificar " + ruta);
            assertFalse(imagen.isError(), ruta + " no es un PNG legible");
            assertEquals(IconoArchivo.ANCHO_BURBUJA, imagen.getWidth(), 0.5,
                    "el icono se pide ya a resolución de burbuja");
            assertEquals(imagen.getWidth(), imagen.getHeight(), 0.5,
                    "los iconos son cuadrados: si no, la burbuja se descuadra");
        }
    }
}
