package com.example.iptv.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Tab
import androidx.compose.material3.TabRow
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import com.example.iptv.data.PlaylistSource

@Composable
fun SetupScreen(
    loading: Boolean,
    error: String?,
    onConnect: (name: String, source: PlaylistSource) -> Unit,
    onCancel: (() -> Unit)? = null,
) {
    var tab by rememberSaveable { mutableIntStateOf(0) }
    var name by rememberSaveable { mutableStateOf("") }
    var m3uUrl by rememberSaveable { mutableStateOf("") }
    var server by rememberSaveable { mutableStateOf("") }
    var user by rememberSaveable { mutableStateOf("") }
    var pass by rememberSaveable { mutableStateOf("") }

    Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
        Column(
            Modifier
                .widthIn(max = 560.dp)
                .fillMaxWidth()
                .verticalScroll(rememberScrollState())
                .padding(24.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp),
        ) {
            Text(if (onCancel == null) "IPTV Player" else "Añadir lista", style = MaterialTheme.typography.headlineMedium)
            Text(
                "Añade la lista de canales que te da tu proveedor IPTV. Puedes guardar varias.",
                style = MaterialTheme.typography.bodyMedium,
            )

            OutlinedTextField(
                value = name, onValueChange = { name = it },
                label = { Text("Nombre de la lista (opcional)") },
                singleLine = true,
                modifier = Modifier.fillMaxWidth(),
            )

            TabRow(selectedTabIndex = tab) {
                Tab(selected = tab == 0, onClick = { tab = 0 }, text = { Text("Lista M3U") })
                Tab(selected = tab == 1, onClick = { tab = 1 }, text = { Text("Xtream Codes") })
            }

            if (tab == 0) {
                OutlinedTextField(
                    value = m3uUrl, onValueChange = { m3uUrl = it },
                    label = { Text("URL de la lista (http://…/lista.m3u)") },
                    singleLine = true,
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Uri),
                    modifier = Modifier.fillMaxWidth(),
                )
            } else {
                OutlinedTextField(
                    value = server, onValueChange = { server = it },
                    label = { Text("Servidor (http://host:puerto)") }, singleLine = true,
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Uri),
                    modifier = Modifier.fillMaxWidth(),
                )
                OutlinedTextField(
                    value = user, onValueChange = { user = it },
                    label = { Text("Usuario") }, singleLine = true, modifier = Modifier.fillMaxWidth(),
                )
                OutlinedTextField(
                    value = pass, onValueChange = { pass = it },
                    label = { Text("Contraseña") }, singleLine = true,
                    visualTransformation = PasswordVisualTransformation(),
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password),
                    modifier = Modifier.fillMaxWidth(),
                )
            }

            if (error != null) {
                Text(error, color = MaterialTheme.colorScheme.error)
            }

            val canConnect = !loading && if (tab == 0) m3uUrl.isNotBlank()
            else server.isNotBlank() && user.isNotBlank() && pass.isNotBlank()

            Button(
                enabled = canConnect,
                onClick = {
                    onConnect(
                        name,
                        if (tab == 0) PlaylistSource.M3u(m3uUrl.trim())
                        else PlaylistSource.Xtream(server.trim(), user.trim(), pass)
                    )
                },
                modifier = Modifier.fillMaxWidth(),
            ) {
                if (loading) CircularProgressIndicator(Modifier.size(20.dp), strokeWidth = 2.dp)
                else Text("Cargar canales")
            }

            if (onCancel != null) {
                OutlinedButton(onClick = onCancel, modifier = Modifier.fillMaxWidth()) { Text("Cancelar") }
            }

            Text(
                "La app no incluye canales: solo reproduce listas a las que tengas acceso legítimo.",
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}
