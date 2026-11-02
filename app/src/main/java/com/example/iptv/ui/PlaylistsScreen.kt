package com.example.iptv.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.Edit
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import com.example.iptv.data.SavedPlaylist

/** "Mis listas": cambiar de lista, añadir, renombrar y borrar. */
@Composable
fun PlaylistsScreen(
    playlists: List<SavedPlaylist>,
    activeId: String?,
    onSelect: (String) -> Unit,
    onAdd: () -> Unit,
    onRename: (String, String) -> Unit,
    onDelete: (String) -> Unit,
    onBack: () -> Unit,
) {
    var toDelete by remember { mutableStateOf<SavedPlaylist?>(null) }
    var toRename by remember { mutableStateOf<SavedPlaylist?>(null) }

    Column(Modifier.fillMaxSize()) {
        Row(Modifier.fillMaxWidth().padding(8.dp), verticalAlignment = Alignment.CenterVertically) {
            IconButton(onClick = onBack) { Icon(Icons.AutoMirrored.Filled.ArrowBack, "Volver") }
            Text("Mis listas", style = MaterialTheme.typography.titleLarge, modifier = Modifier.weight(1f))
        }

        LazyColumn(
            Modifier.weight(1f),
            contentPadding = PaddingValues(horizontal = 16.dp, vertical = 8.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            items(playlists, key = { it.id }) { p ->
                val active = p.id == activeId
                val shape = RoundedCornerShape(12.dp)
                Row(
                    Modifier
                        .fillMaxWidth()
                        .clip(shape)
                        .background(MaterialTheme.colorScheme.surface)
                        .border(2.dp, if (active) MaterialTheme.colorScheme.primary else Color.Transparent, shape)
                        .clickable { onSelect(p.id) }
                        .padding(start = 16.dp, top = 8.dp, bottom = 8.dp, end = 4.dp),
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    if (active) {
                        Icon(Icons.Default.CheckCircle, "Lista activa", tint = MaterialTheme.colorScheme.primary)
                        Spacer(Modifier.width(12.dp))
                    }
                    Column(Modifier.weight(1f)) {
                        Text(p.name, style = MaterialTheme.typography.titleMedium, maxLines = 1, overflow = TextOverflow.Ellipsis)
                        Text(
                            p.description, style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            maxLines = 1, overflow = TextOverflow.Ellipsis,
                        )
                    }
                    IconButton(onClick = { toRename = p }) { Icon(Icons.Default.Edit, "Renombrar ${p.name}") }
                    IconButton(onClick = { toDelete = p }) { Icon(Icons.Default.Delete, "Borrar ${p.name}") }
                }
            }
        }

        Button(onClick = onAdd, modifier = Modifier.fillMaxWidth().padding(16.dp)) {
            Icon(Icons.Default.Add, null)
            Spacer(Modifier.width(8.dp))
            Text("Añadir lista")
        }
    }

    toDelete?.let { p ->
        AlertDialog(
            onDismissRequest = { toDelete = null },
            title = { Text("¿Borrar \"${p.name}\"?") },
            text = { Text("Se quitará de tus listas guardadas. Tus favoritos se mantienen.") },
            confirmButton = { TextButton(onClick = { onDelete(p.id); toDelete = null }) { Text("Borrar") } },
            dismissButton = { TextButton(onClick = { toDelete = null }) { Text("Cancelar") } },
        )
    }

    toRename?.let { p ->
        var name by remember(p.id) { mutableStateOf(p.name) }
        AlertDialog(
            onDismissRequest = { toRename = null },
            title = { Text("Renombrar lista") },
            text = {
                OutlinedTextField(value = name, onValueChange = { name = it }, singleLine = true, label = { Text("Nombre") })
            },
            confirmButton = {
                TextButton(enabled = name.isNotBlank(), onClick = { onRename(p.id, name); toRename = null }) { Text("Guardar") }
            },
            dismissButton = { TextButton(onClick = { toRename = null }) { Text("Cancelar") } },
        )
    }
}
