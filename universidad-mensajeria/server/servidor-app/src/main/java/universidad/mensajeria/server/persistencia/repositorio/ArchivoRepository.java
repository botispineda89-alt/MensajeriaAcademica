package universidad.mensajeria.server.persistencia.repositorio;

import org.springframework.data.jpa.repository.JpaRepository;
import universidad.mensajeria.server.persistencia.entidades.Archivo;

import java.util.Optional;

public interface ArchivoRepository extends JpaRepository<Archivo, Long> {

    Optional<Archivo> findFirstByHashSha256(String hashSha256);

    Optional<Archivo> findFirstByRuta(String ruta);

    Optional<Archivo> findFirstByPropietario_CodigoAndHashSha256(String codigo,
                                                                  String hashSha256);
}
